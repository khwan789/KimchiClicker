using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Attach this to a single GameObject called "Game".
public class KimchiGame : MonoBehaviour
{
    public static KimchiGame Instance { get; private set; }

    private float uiPulseAccum = 0f;   // UI refresh accumulator
    public float uiPulseInterval = 0.1f; // refresh every 0.1s (~10 Hz)
    private bool _hasLoaded = false;

    [Header("Icons")]
    public Sprite[] materialIcons;   // size = 5 (고춧가루, 마늘, 생강, 파, 액젓)
    public Sprite[] recipeIcons;     // size = recipeNames.Length

    [Header("Tick")]
    public float logicHz = 10f;   // CPS -> kimchi gain at 10 ticks/sec
    private float tickAccum;

    [Header("Currencies")]
    public BigNumber kimchi = BigNumber.Zero;    // basic
    public long coins = 0;                       // 냥
    public long goldKeys = 0;                    // 유료 재화 (drip + IAP)

    [Header("Tap")]
    public int manualBasePerTap = 1; // replaced by sum of producer levels (min 1)
    public BigNumber lastTapGain = BigNumber.One;

    [Header("Auto Producers (10)")]
    public List<ProducerDef> producers = new List<ProducerDef>();
    public List<ProducerState> states = new List<ProducerState>();
    public int buyAmount = 1; // 1 / 10 / 100

    [Header("Materials (coins) 5 items, +1% each level")]
    public string[] materialNames = { "Chili", "Garlic", "Ginger", "Scallion", "Fish Sauce" };
    public int[] materialLv = new int[5];
    public long[] materialCost = new long[5]; // starts at 100, ×10 per level

    [Header("Recipes (gold keys)")]
    public string[] recipeNames = { "Kkakdugi", "Mul Kimchi", "Buchu", "Oi Sobagi", "Pa", "Yeolmu" };
    public int[] recipeLv; // cap 10; +100% each level
    public int[] recipeClaimed; // achievement claim cursor
    public long[] recipeCost; // starts at 100 keys, ×2 per level

    [Header("Storage / Prestige unlocks (CPS thresholds)")]
    public StorageStage[] storage = new StorageStage[5];
    public int unlockedStorageCount = 0; // current run
    public int permanentBonusPct = 0;    // persists across prestiges
    public bool prestigeAvailable = false;

    [Header("Ad Buffs / Shop")]
    public int adBuffPct = 0;         // +100% when active
    public float adBuffRemain = 0f;   // seconds
    public int adCoinDailyLimit = 5;
    public int adCoinDailyUsed = 0;
    public string adCoinDate = "";    // YYYYMMDD

    [Header("Offline gain caps")]
    public float activeSeconds = 0f; // existing
    public int offlineKimchiCapHours = 2;   // <= cap kimchi offline gains to 2 hours
    public bool countOfflineForGoldKeys = true;   // NEW: include offline time
    public int offlineKeyCapHours = 24;           // NEW: cap per resume (0 = no cap)

    [Header("Achievements (producers)")]
    public int[] producerClaimedMilestones; // count of milestones already claimed per producer

    [Header("UI hooks (optional)")]
    public Action OnAnyChanged; // assign from a UI script to refresh labels

    const double COST_GAIN_RATIO = 1.2; // producer cost growth ratio

    [Serializable]
    public class ProducerDef
    {
        public string name;
        public BigNumber baseRate; // base CPS per level before (1 + L*1%) and multipliers
        public BigNumber baseCost;
        public Sprite icon;
    }

    [Serializable]
    public class ProducerState
    {
        public int level;
    }

    [Serializable]
    public class StorageStage
    {
        public string name;
        public BigNumber cpsThreshold; // e.g., 1C/s, 1F/s...
        public bool unlocked;          // this run
        public bool claimed;           // NEW: achievement claimed (persists across prestiges)
        public int buffPct;            // +100% each
        public int coinReward;         // 냥 on unlock
        public Sprite icon;
    }

    #region Unity
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Application.targetFrameRate = 60;

        InitDefsIfEmpty();

        // Load first. If no file yet, initialize and create a baseline save.
        if (File.Exists(PathFile()))
        {
            Load();
        }
        else
        {
            InitProgress();
            SyncDataShapes();
            _hasLoaded = true;       // <-- ADD THIS
            Changed();
            Save();                  // <-- this will now actually write the file
            Debug.Log("[Kimchi] First run: created fresh save at " + PathFile());
        }

        InvokeRepeating(nameof(AutoSave), 10f, 30f);
    }


    void Update()
    {
        // logic ticks
        tickAccum += Time.unscaledDeltaTime;
        float tickInterval = 1f / logicHz;
        while (tickAccum >= tickInterval)
        {
            tickAccum -= tickInterval;
            Tick(tickInterval);
        }

        // ad buff timer
        if (adBuffRemain > 0f)
        {
            adBuffRemain -= Time.unscaledDeltaTime;
            if (adBuffRemain <= 0f) { adBuffRemain = 0f; adBuffPct = 0; Changed(); }
        }

        // gold key drip (active playtime)
        activeSeconds += Time.unscaledDeltaTime;
        if (activeSeconds >= 3600f)
        {
            goldKeys += 1;
            activeSeconds -= 3600f;
            Changed();
        }
    }

    void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            Save();
        }
        else
        {
            string lastBinStr = PlayerPrefs.GetString("lastUtc", "0");
            if (long.TryParse(lastBinStr, out long lastBin) && lastBin != 0L)
            {
                DateTime last = DateTime.FromBinary(lastBin);
                double seconds = (DateTime.UtcNow - last).TotalSeconds;

                if (seconds > 1 && seconds < 7 * 24 * 3600) // global sanity cap
                {
                    // ---- 1) Offline kimchi (cap to 2 hours by default) ----
                    double kimchiSeconds = seconds;
                    if (offlineKimchiCapHours > 0)
                        kimchiSeconds = Math.Min(kimchiSeconds, offlineKimchiCapHours * 3600.0);

                    var gain = CPS() * kimchiSeconds; // uses current CPS snapshot
                    kimchi = kimchi + gain;

                    // ---- 2) Offline gold keys (optional, separate cap) ----
                    if (countOfflineForGoldKeys)
                    {
                        double add = seconds;
                        if (offlineKeyCapHours > 0)
                            add = Math.Min(add, offlineKeyCapHours * 3600.0);

                        activeSeconds += (float)add;

                        int keysGained = 0;
                        while (activeSeconds >= 3600f)
                        {
                            goldKeys += 1;
                            activeSeconds -= 3600f;
                            keysGained++;
                        }
                    }
                }
            }
            Changed();
            Save(); // persist and refresh lastUtc
        }
    }



    void OnDisable()
    {
        if (Application.isPlaying && Instance == this && _hasLoaded) Save();
    }



    void OnApplicationQuit() => Save();
    #endregion

    #region Init
    void InitDefsIfEmpty()
    {
        if (producers.Count > 0) return;
        // Name, baseRate, startCost
        producers = new List<ProducerDef>
        {
            new ProducerDef{ name="한국인",        baseRate=BigNumber.FromDouble(1),       baseCost=BigNumber.FromDouble(100) },
            new ProducerDef{ name="식당",          baseRate=BigNumber.FromDouble(100),     baseCost=BigNumber.FromSuffix(1,1) }, // 1A
            new ProducerDef{ name="마트",          baseRate=BigNumber.FromSuffix(1,1),     baseCost=BigNumber.FromSuffix(1,3) }, // 1C
            new ProducerDef{ name="공장",          baseRate=BigNumber.FromSuffix(1,2),     baseCost=BigNumber.FromSuffix(1,5) }, // 1B, 1E
            new ProducerDef{ name="외국인 러버",   baseRate=BigNumber.FromSuffix(1,3),     baseCost=BigNumber.FromSuffix(1,7) }, // 1C, 1G
            new ProducerDef{ name="정부",          baseRate=BigNumber.FromSuffix(1,4),     baseCost=BigNumber.FromSuffix(1,9) }, // 1D, 1I
            new ProducerDef{ name="세계 정부",     baseRate=BigNumber.FromSuffix(1,5),     baseCost=BigNumber.FromSuffix(1,11)}, // 1E, 1K
            new ProducerDef{ name="지구",          baseRate=BigNumber.FromSuffix(1,6),     baseCost=BigNumber.FromSuffix(1,13)}, // 1F, 1M
            new ProducerDef{ name="태양계",        baseRate=BigNumber.FromSuffix(1,7),     baseCost=BigNumber.FromSuffix(1,15)}, // 1G, 1O
            new ProducerDef{ name="외계인",        baseRate=BigNumber.FromSuffix(1,8),     baseCost=BigNumber.FromSuffix(1,17)}, // 1H, 1Q
        };

        states = new List<ProducerState>();
        for (int i = 0; i < producers.Count; i++) states.Add(new ProducerState());

        // storage thresholds: 1C, 1F, 1I, 1L, 1O per second
        storage = new StorageStage[]
        {
            new StorageStage{ name="김치 통조림",      cpsThreshold=BigNumber.FromSuffix(1,3),  buffPct=100, coinReward=100 },
            new StorageStage{ name="포기 김치",        cpsThreshold=BigNumber.FromSuffix(1,6),  buffPct=100, coinReward=300 },
            new StorageStage{ name="플라스틱 용기",    cpsThreshold=BigNumber.FromSuffix(1,9),  buffPct=100, coinReward=500 },
            new StorageStage{ name="김치 옹기",        cpsThreshold=BigNumber.FromSuffix(1,12), buffPct=100, coinReward=750 },
            new StorageStage{ name="김치 냉장기",      cpsThreshold=BigNumber.FromSuffix(1,15), buffPct=100, coinReward=1000 },
        };

        foreach (var s in storage) { s.unlocked = false; s.claimed = false; } // claimed defaults false
    }

    void InitProgress()
    {
        // materials
        for (int i = 0; i < materialLv.Length; i++)
        {
            materialLv[i] = 0;
            materialCost[i] = 100; // coins
        }
        // recipes
        recipeLv = new int[recipeNames.Length];
        recipeClaimed = new int[recipeNames.Length];
        recipeCost = new long[recipeNames.Length];
        for (int i = 0; i < recipeNames.Length; i++)
        {
            recipeLv[i] = 0;
            recipeClaimed[i] = 0;
            recipeCost[i] = 100; // keys
        }
        // achievements
        producerClaimedMilestones = new int[producers.Count]; // counts (first obtain + per 10 levels)
        // storage state
        foreach (var s in storage) { 
            s.unlocked = false; 
            s.claimed = false; 
        }
        unlockedStorageCount = 0;
        prestigeAvailable = false;
    }

    // === Contribution helpers ===
    // Materials: +1% per level (per item)
    public int MaterialContributionPct(int i)
    {
        return (i >= 0 && i < materialLv.Length) ? materialLv[i] : 0;
    }

    // Recipes: +100% per level
    public int RecipeContributionPct(int i)
    {
        return (i >= 0 && i < recipeLv.Length) ? recipeLv[i] * 100 : 0;
    }

    // Storage/Containers: +buffPct% when unlocked, 0 if locked
    public int StorageContributionPct(int i)
    {
        return (i >= 0 && i < storage.Length) ? (storage[i].unlocked ? storage[i].buffPct : 0) : 0;
    }

    // Total % that forms the global multiplier: 1 + (totalPct / 100)
    public int TotalContributionPct()
    {
        int materialsPct = 0; for (int m = 0; m < materialLv.Length; m++) materialsPct += materialLv[m];
        int recipesPct = 0; for (int r = 0; r < recipeLv.Length; r++) recipesPct += recipeLv[r] * 100;
        int storagePct = 0; for (int s = 0; s < storage.Length; s++) if (storage[s].unlocked) storagePct += storage[s].buffPct;
        return materialsPct + recipesPct + storagePct + adBuffPct + permanentBonusPct;
    }

    // (Optional) share of total, as a %
    public double ShareOfTotal(int partPct)
    {
        int total = TotalContributionPct();
        return (total > 0) ? (partPct * 100.0) / total : 0.0;
    }

    #endregion

    #region Core math
    // Global multiplier = 1 + (materials + recipes + storage unlocked + ad buff + permanent)/100
    public double GlobalMultiplier()
    {
        int materialsPct = 0;
        for (int i = 0; i < materialLv.Length; i++) materialsPct += materialLv[i]; // +1% per level per item

        int recipesPct = 0;
        for (int i = 0; i < recipeLv.Length; i++) recipesPct += recipeLv[i] * 100;

        int storagePct = 0;
        for (int i = 0; i < storage.Length; i++) if (storage[i].unlocked) storagePct += storage[i].buffPct;

        int totalPct = materialsPct + recipesPct + storagePct + adBuffPct + permanentBonusPct;
        return 1.0 + totalPct / 100.0;
    }

    // CPS: sum_i baseRate * L * (1 + L*1%) * global
    // Total CPS (buffed)
    public BigNumber CPS()
    {
        BigNumber sumBase = BigNumber.Zero;
        int n = Mathf.Min(producers.Count, states.Count);
        for (int i = 0; i < n; i++)
            sumBase = sumBase + ProducerBaseCPS(i);

        return sumBase * GlobalMultiplier();
    }

    // Tap: max(1, sum of all producer levels) * global
    public void OnTap()
    {
        int sumLv = 0;
        for (int i = 0; i < states.Count; i++) sumLv += (states[i].level * (int)(1 + 0.1 * states[i].level));
        int baseTap = Mathf.Max(manualBasePerTap, sumLv);
        lastTapGain = BigNumber.FromDouble(baseTap) * GlobalMultiplier();
        kimchi = kimchi + lastTapGain;
        Changed();
    }

    // logic tick
    void Tick(float seconds)
    {
        var gain = CPS() * seconds;
        kimchi = kimchi + gain;

        // prestige available?
        prestigeAvailable = (unlockedStorageCount >= storage.Length);

        uiPulseAccum += seconds;
        if (uiPulseAccum >= uiPulseInterval)
        {
            uiPulseAccum = 0f;
            Changed();  // tells UI to refresh labels like kimchi, CPS, etc.
        }
    }

    // Base (no global multiplier)
    public BigNumber ProducerBaseCPSAtLevel(int index, int level)
    {
        if (index < 0 || index >= producers.Count || level <= 0) return BigNumber.Zero;
        var def = producers[index];
        double selfMult = level * (1.0 + 0.1 * level); // L * (1 + 1% * L)
        return def.baseRate * selfMult;                 // <-- NO GlobalMultiplier here
    }

    // Convenience
    public BigNumber ProducerBaseCPS(int index) =>
        ProducerBaseCPSAtLevel(index, states[index].level);

    // Final (buffed)
    public BigNumber ProducerCPS(int index) =>
        ProducerBaseCPS(index) * GlobalMultiplier();

    // If buying k levels
    public BigNumber ProducerCPSDiffForPurchase(int index, int k)
    {
        int L = states[index].level;
        var before = ProducerBaseCPSAtLevel(index, L);
        var after = ProducerBaseCPSAtLevel(index, L + k);
        return (after - before) * GlobalMultiplier();
    }

    void EnsureArrayWithDefault(ref int[] arr, int targetLen, int defVal)
    {
        if (arr == null) { arr = new int[targetLen]; for (int i = 0; i < targetLen; i++) arr[i] = defVal; return; }
        if (arr.Length == targetLen) return;
        var newArr = new int[targetLen];
        int copy = Mathf.Min(arr.Length, targetLen);
        Array.Copy(arr, newArr, copy);
        for (int i = copy; i < targetLen; i++) newArr[i] = defVal;
        arr = newArr;
    }
    void EnsureArrayWithDefault(ref long[] arr, int targetLen, long defVal)
    {
        if (arr == null) { arr = new long[targetLen]; for (int i = 0; i < targetLen; i++) arr[i] = defVal; return; }
        if (arr.Length == targetLen) return;
        var newArr = new long[targetLen];
        int copy = Mathf.Min(arr.Length, targetLen);
        Array.Copy(arr, newArr, copy);
        for (int i = copy; i < targetLen; i++) newArr[i] = defVal;
        arr = newArr;
    }
    void SyncDataShapes()
    {
        // states list length == producers count
        if (states == null) states = new List<ProducerState>();
        while (states.Count < producers.Count) states.Add(new ProducerState());
        if (states.Count > producers.Count) states.RemoveRange(producers.Count, states.Count - producers.Count);

        // producer milestone claims length
        if (producerClaimedMilestones == null || producerClaimedMilestones.Length != producers.Count)
        {
            var newArr = new int[producers.Count];
            if (producerClaimedMilestones != null)
                Array.Copy(producerClaimedMilestones, newArr, Mathf.Min(producerClaimedMilestones.Length, newArr.Length));
            producerClaimedMilestones = newArr;
        }

        // materials (5)
        EnsureArrayWithDefault(ref materialLv, 5, 0);
        EnsureArrayWithDefault(ref materialCost, 5, 100);

        // recipes (names length)
        EnsureArrayWithDefault(ref recipeLv, recipeNames.Length, 0);
        EnsureArrayWithDefault(ref recipeClaimed, recipeNames.Length, 0);
        EnsureArrayWithDefault(ref recipeCost, recipeNames.Length, 100);
    }

    #endregion

    #region Producers: costs, buy, achievements
    double PowFast(double a, int n) => Math.Pow(a, n);

    // Geometric sum for buying K levels at ratio r:
    // cost = baseCost * r^N * (r^K - 1) / (r - 1)
    public BigNumber CostToBuy(int index, int k)
    {
        var def = producers[index];
        int N = states[index].level;
        double r = COST_GAIN_RATIO;

        double rPowN = PowFast(r, N);
        double rPowK = PowFast(r, k);
        double mult = rPowN * (rPowK - 1.0) / (r - 1.0);

        return def.baseCost * mult;
    }

    public bool CanAfford(BigNumber cost) => kimchi >= cost;

    public void TryBuyProducer(int index)
    {
        int k = buyAmount;
        var cost = CostToBuy(index, k);
        if (!CanAfford(cost)) return;

        kimchi = kimchi - cost;
        int prevLv = states[index].level;
        states[index].level += k;
        Changed();
        Save();
        // Achievements: producer first obtain + per 10 levels (claimable)
        // We *queue* milestones; user claims later for coins (10 per milestone)
        // milestone definition: 1st obtain (level>=1) and every 10 levels.
        // we just update counts on Claim(), so nothing to do here.
    }

    public void SetBuyAmount(int amount) { buyAmount = Mathf.Clamp(amount, 1, 100); Changed(); }
    #endregion

    #region Materials (coins)
    public void TryUpgradeMaterial(int i)
    {
        if (i < 0 || i >= materialLv.Length) return;
        long cost = materialCost[i];
        if (coins < cost) return;

        coins -= cost;
        materialLv[i] += 1;
        materialCost[i] *= 10; // x10 per level
        Changed();
        Save();
    }
    #endregion

    #region Recipes (gold keys, cap 10)
    public void TryUpgradeRecipe(int i)
    {
        if (i < 0 || i >= recipeLv.Length) return;
        if (recipeLv[i] >= 10) return;

        long cost = recipeCost[i];
        if (goldKeys < cost) return;

        goldKeys -= cost;
        recipeLv[i] += 1;
        recipeCost[i] *= 2; // doubling
        Changed();
        Save();
    }
    #endregion

    #region Storage / Prestige
    public void TryPrestige()
    {
        if (!prestigeAvailable) return;

        // Add permanent +500% (sum of five +100%)
        permanentBonusPct += 500;

        // Reset run:
        kimchi = BigNumber.Zero;
        for (int i = 0; i < states.Count; i++) states[i].level = 0;

        // Reset storage unlocks for the new run
        foreach (var s in storage) s.unlocked = false;
        unlockedStorageCount = 0;
        prestigeAvailable = false;

        // *** Spec says "kept after reset": coins, keys, material levels, achievements
        // We DO NOT reset: coins, goldKeys, materialLv, producerClaimedMilestones, recipeClaimed (we keep)
        // We DO reset: recipe levels? (Spec doesn't list recipes as kept → reset)
        for (int i = 0; i < recipeLv.Length; i++)
        {
            recipeLv[i] = 0;
            recipeCost[i] = 100;
            // Keep recipeClaimed intact so claimed milestones persist as history baseline.
        }

        Changed();
        Save();
    }

    public bool CanUnlockStorage(int index)
    {
        if (index < 0 || index >= storage.Length) return false;
        var s = storage[index];
        if (s.unlocked) return false;

        var perSec = CPS(); // BigNumber
        return perSec.CompareTo(s.cpsThreshold) >= 0; // perSec >= threshold
    }

    public bool TryUnlockStorage(int index)
    {
        if (!CanUnlockStorage(index)) return false;

        storage[index].unlocked = true;
        unlockedStorageCount++;
        Changed();
        Save();
        return true;
    }
    #endregion

    #region Shop / Ads
    public void Shop_WatchAdBuff()
    {
        // +100% for 5 minutes, stack not additive (refresh if stronger)
        adBuffPct = 100;
        float baseSec = 300f;
        // Each 10 ad watches add +10s up to +10min? Spec: +10s per 10 times, cap 10min total.
        // Keep simple: extend by +10s per 10 uses of THIS button. Track in PlayerPrefs.
        int watched = PlayerPrefs.GetInt("adBuffWatched", 0) + 1;
        PlayerPrefs.SetInt("adBuffWatched", watched);
        float bonus = Mathf.Min((watched / 10) * 10f, 300f); // cap +300s
        adBuffRemain = baseSec + bonus;
        Changed();
        Save();
    }

    public void Shop_WatchAdCoins()
    {
        string today = DateTime.Now.ToString("yyyyMMdd");
        if (adCoinDate != today)
        {
            adCoinDate = today; adCoinDailyUsed = 0;
        }
        if (adCoinDailyUsed >= adCoinDailyLimit) return;

        adCoinDailyUsed++;
        coins += 20;
        Changed();
        Save();
    }

    public void Shop_WatchAd5MinKimchi()
    {
        // current CPS * 300
        var gain = CPS() * 300.0;
        kimchi = kimchi + gain;
        Changed();
        Save();
    }

    public void Shop_BuyKeys100() { goldKeys += 100; Changed(); Save(); } // IAP placeholder
    #endregion

    #region Achievements (claimable)
    // Producers: first obtain (level>=1) and each 10 levels → +10 coins per milestone
    public void ClaimProducerMilestones(int index)
    {
        int L = states[index].level;
        int milestones = (L >= 1 ? 1 : 0) + (L / 10);
        int already = producerClaimedMilestones[index];
        int claim = Mathf.Max(0, milestones - already);
        if (claim <= 0) return;

        producerClaimedMilestones[index] += claim;
        coins += 10 * claim;
        Changed();
        Save();
    }

    // Recipes: “최초 획득 및 강화 시마다 동전 100개씩” → each level counts
    public void ClaimRecipeMilestones(int index)
    {
        int L = recipeLv[index];
        int already = recipeClaimed[index];
        int claim = Mathf.Max(0, L - already);
        if (claim <= 0) return;

        recipeClaimed[index] += claim;
        coins += 100 * claim;
        Changed();
        Save();
    }

    public bool StorageClaimAvailable(int index)
    {
        if (index < 0 || index >= storage.Length) return false;
        var s = storage[index];
        return s.unlocked && !s.claimed;
    }

    public void ClaimStorageMilestone(int index)
    {
        if (index < 0 || index >= storage.Length) return;
        var s = storage[index];
        if (!s.unlocked || s.claimed) return;

        s.claimed = true;
        coins += s.coinReward;
        Changed();
        Save();
    }

    public int ClaimAllStorageMilestones()
    {
        int claimedNow = 0;
        for (int i = 0; i < storage.Length; i++)
        {
            if (StorageClaimAvailable(i))
            {
                storage[i].claimed = true;
                coins += storage[i].coinReward;
                claimedNow++;
            }
        }
        if (claimedNow > 0) { Changed(); Save(); }
        return claimedNow;
    }

    // === Achievements helpers ===
    public int ProducerClaimableCount(int index)
    {
        int L = states[index].level;
        int milestones = (L >= 1 ? 1 : 0) + (L / 10);
        int already = producerClaimedMilestones[index];
        return Mathf.Max(0, milestones - already);
    }

    public int RecipeClaimableCount(int index)
    {
        int L = recipeLv[index];
        int already = recipeClaimed[index];
        return Mathf.Max(0, L - already);
    }

    // Storage already has: StorageClaimAvailable(int i) and ClaimStorageMilestone(int i)

    // Preview totals for a “Claim All” button
    public (int totalCoins, int totalItems) PreviewClaimAllAchievements()
    {
        int coins = 0, items = 0;

        // Producers: 10 coins per milestone
        for (int i = 0; i < producers.Count; i++)
        {
            int c = ProducerClaimableCount(i);
            if (c > 0) { coins += 10 * c; items += c; }
        }

        // Recipes: 100 coins per level
        for (int i = 0; i < recipeLv.Length; i++)
        {
            int c = RecipeClaimableCount(i);
            if (c > 0) { coins += 100 * c; items += c; }
        }

        // Storage: each claim once, reward = coinReward
        for (int i = 0; i < storage.Length; i++)
        {
            if (StorageClaimAvailable(i)) { coins += storage[i].coinReward; items += 1; }
        }

        return (coins, items);
    }

    // Claim everything in one click
    public (int coins, int producerClaims, int recipeClaims, int storageClaims) ClaimAllAchievements()
    {
        int gainedCoins = 0, pc = 0, rc = 0, sc = 0;

        // Producers
        for (int i = 0; i < producers.Count; i++)
        {
            int c = ProducerClaimableCount(i);
            if (c > 0)
            {
                producerClaimedMilestones[i] += c;
                gainedCoins += 10 * c;
                pc += c;
            }
        }

        // Recipes
        for (int i = 0; i < recipeLv.Length; i++)
        {
            int c = RecipeClaimableCount(i);
            if (c > 0)
            {
                recipeClaimed[i] += c;
                gainedCoins += 100 * c;
                rc += c;
            }
        }

        // Storage
        for (int i = 0; i < storage.Length; i++)
        {
            if (StorageClaimAvailable(i))
            {
                storage[i].claimed = true;
                gainedCoins += storage[i].coinReward;
                sc += 1;
            }
        }

        if (gainedCoins > 0)
        {
            coins += gainedCoins;
            Changed();
            Save();
        }
        return (gainedCoins, pc, rc, sc);
    }

    #endregion

    #region Utility
    // Buying cost label for current buyAmount
    public BigNumber PreviewCost(int index) => CostToBuy(index, buyAmount);

    public BigNumber CPS_ManualTapPreview()
    {
        int sumLv = 0; for (int i = 0; i < states.Count; i++) sumLv += (states[i].level * (int)(1 + 0.1 * states[i].level));
        int baseTap = Mathf.Max(manualBasePerTap, sumLv);
        return BigNumber.FromDouble(baseTap) * GlobalMultiplier();
    }

    void Changed() { OnAnyChanged?.Invoke(); }
    #endregion

    #region Save/Load
    [Serializable]
    class SaveData
    {
        public double kimchiMantissa;
        public int kimchiTier;
        public long coins;
        public long goldKeys;

        public int[] producerLv;
        public int[] producerClaimed;

        public int[] materialLv;
        public long[] materialCost;

        public int[] recipeLv;
        public int[] recipeClaimed;
        public long[] recipeCost;

        public bool[] storageUnlocked;
        public int unlockedStorageCount;
        public bool[] storageClaimed;
        public int permanentBonusPct;

        public int adBuffPct;
        public float adBuffRemain;
        public int adCoinDailyUsed;
        public string adCoinDate;

        public float activeSeconds;
    }

    string PathFile() => Path.Combine(Application.persistentDataPath, "kimchi_save.json");

    public void Save()
    {
        if (!_hasLoaded) return;   // don't write an empty file before Load()
        var d = new SaveData
        {
            kimchiMantissa = kimchi.mantissa,
            kimchiTier = kimchi.tier,
            coins = coins,
            goldKeys = goldKeys,
            producerLv = states.ConvertAll(s => s.level).ToArray(),
            producerClaimed = (int[])producerClaimedMilestones.Clone(),
            materialLv = (int[])materialLv.Clone(),
            materialCost = (long[])materialCost.Clone(),
            recipeLv = (int[])recipeLv.Clone(),
            recipeClaimed = (int[])recipeClaimed.Clone(),
            recipeCost = (long[])recipeCost.Clone(),
            storageUnlocked = Array.ConvertAll(storage, s => s.unlocked),
            unlockedStorageCount = unlockedStorageCount,
            storageClaimed = Array.ConvertAll(storage, s => s.claimed),
            permanentBonusPct = permanentBonusPct,
            adBuffPct = adBuffPct,
            adBuffRemain = adBuffRemain,
            adCoinDailyUsed = adCoinDailyUsed,
            adCoinDate = adCoinDate,
            activeSeconds = activeSeconds
        };
        try
        {
            var path = PathFile();
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(path, JsonUtility.ToJson(d));
            Debug.Log($"[Kimchi] Saved -> {path} | Lvs=[{string.Join(",", states.ConvertAll(s => s.level))}]");

            PlayerPrefs.SetString("lastUtc", DateTime.UtcNow.ToBinary().ToString());
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogError("[Kimchi] Save failed: " + e);
        }
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(PathFile()))
            {
                _hasLoaded = true;
                Changed();
                return;
            }

            var json = File.ReadAllText(PathFile());
            var d = JsonUtility.FromJson<SaveData>(json);

            // Always size collections first
            SyncDataShapes();

            // Currencies
            kimchi = new BigNumber(d.kimchiMantissa, d.kimchiTier);
            coins = d.coins;
            goldKeys = d.goldKeys;

            // Producers (null/length guard)
            if (d.producerLv != null)
            {
                int n = Mathf.Min(states.Count, d.producerLv.Length);
                for (int i = 0; i < n; i++)
                    states[i].level = d.producerLv[i];
            }

            // Producer achievement claims
            producerClaimedMilestones = d.producerClaimed ?? new int[producers.Count];

            // Materials
            materialLv = d.materialLv ?? materialLv;
            materialCost = d.materialCost ?? materialCost;

            // Recipes
            recipeLv = d.recipeLv ?? recipeLv;
            recipeClaimed = d.recipeClaimed ?? new int[recipeLv.Length];
            recipeCost = d.recipeCost ?? recipeCost;

            // Storage
            if (d.storageUnlocked != null)
                for (int i = 0; i < storage.Length && i < d.storageUnlocked.Length; i++)
                    storage[i].unlocked = d.storageUnlocked[i];

            if (d.storageClaimed != null)
                for (int i = 0; i < storage.Length && i < d.storageClaimed.Length; i++)
                    storage[i].claimed = d.storageClaimed[i];
            else
                for (int i = 0; i < storage.Length; i++)
                    storage[i].claimed = storage[i].unlocked; // migration

            unlockedStorageCount = d.unlockedStorageCount;
            permanentBonusPct = d.permanentBonusPct;

            adBuffPct = d.adBuffPct;
            adBuffRemain = d.adBuffRemain;
            adCoinDailyUsed = d.adCoinDailyUsed;
            adCoinDate = d.adCoinDate ?? "";
            activeSeconds = d.activeSeconds;

            _hasLoaded = true;
            Changed();
            Debug.Log($"[Kimchi] Loaded <- {PathFile()} | Lvs=[{string.Join(",", states.ConvertAll(s => s.level))}]");
        }
        catch (Exception e)
        {
            Debug.LogWarning("Load failed: " + e.Message);
            InitProgress();
            SyncDataShapes();
            _hasLoaded = true;
            Changed();
        }
    }


    bool LoadOrInit()
    {
        try
        {
            if (!File.Exists(PathFile())) return false;

            var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(PathFile()));

            kimchi = new BigNumber(d.kimchiMantissa, d.kimchiTier);
            coins = d.coins;
            goldKeys = d.goldKeys;

            for (int i = 0; i < states.Count && i < d.producerLv.Length; i++)
                states[i].level = d.producerLv[i];

            producerClaimedMilestones = d.producerClaimed ?? new int[producers.Count];

            materialLv = d.materialLv ?? materialLv;
            materialCost = d.materialCost ?? materialCost;

            recipeLv = d.recipeLv ?? recipeLv;
            recipeClaimed = d.recipeClaimed ?? new int[recipeLv.Length];
            recipeCost = d.recipeCost ?? recipeCost;

            if (d.storageUnlocked != null)
                for (int i = 0; i < storage.Length && i < d.storageUnlocked.Length; i++)
                    storage[i].unlocked = d.storageUnlocked[i];

            if (d.storageClaimed != null)
            {
                for (int i = 0; i < storage.Length && i < d.storageClaimed.Length; i++)
                    storage[i].claimed = d.storageClaimed[i];
            }
            else
            {
                for (int i = 0; i < storage.Length; i++)
                    storage[i].claimed = storage[i].unlocked;
            }

            unlockedStorageCount = d.unlockedStorageCount;
            permanentBonusPct = d.permanentBonusPct;

            adBuffPct = d.adBuffPct;
            adBuffRemain = d.adBuffRemain;
            adCoinDailyUsed = d.adCoinDailyUsed;
            adCoinDate = d.adCoinDate ?? "";
            activeSeconds = d.activeSeconds;

            SyncDataShapes();
            _hasLoaded = true;
            Changed();

            Debug.Log($"[Kimchi] Loaded <- {PathFile()} | Lvs=[{string.Join(",", states.ConvertAll(s => s.level))}]");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Load failed: " + e.Message);
            return false;
        }
    }

    void AutoSave() => Save();
    #endregion
}
