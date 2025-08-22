using UnityEngine;
using TMPro;
using UnityEngine.UIElements;

public class KimchiUI : MonoBehaviour
{
    public TextMeshProUGUI txtKimchi, txtCoins, txtKeys, txtCPS, txtTap, txtBuff;
    float localAccum = 0f;
    public GameObject[] tabs = new GameObject[0];
    public TextMeshProUGUI[] prodBuys = new TextMeshProUGUI[0];

    void Start()
    {
        KimchiGame.Instance.OnAnyChanged += Refresh;
        Refresh();

        if (tabs != null)
        {
            tabs[0].gameObject.SetActive(true);
            for (int i = 1; i < tabs.Length; i++)
            {
                tabs[i].gameObject.SetActive(false);
            }
        }

        if (prodBuys != null)
        {
            prodBuys[0].color = Color.white;
            for (int i = 1; i < prodBuys.Length; i++)
            {
                prodBuys[i].color = Color.grey;
            }
        }
    }

    void OnDestroy()
    {
        if (KimchiGame.Instance != null) KimchiGame.Instance.OnAnyChanged -= Refresh;
    }

    void Update()
    {
        localAccum += Time.unscaledDeltaTime;
        if (localAccum >= 0.25f)
        {
            localAccum = 0f;
            Refresh();
        }
    }

    void Refresh()
    {
        var g = KimchiGame.Instance;
        if (txtKimchi) txtKimchi.text = $"Kimchi: {g.kimchi}";
        if (txtCoins) txtCoins.text = $"Coins(³É): {g.coins:N0}";
        if (txtKeys) txtKeys.text = $"Gold Keys: {g.goldKeys:N0}";
        if (txtCPS) txtCPS.text = $"CPS: {g.CPS()}";
        if (txtTap) txtTap.text = $"Tap: {g.CPS_ManualTapPreview()}";
        if (txtBuff) txtBuff.text = (g.adBuffRemain > 0) ? $"+{g.adBuffPct}% {g.adBuffRemain:0}s" : "No Buff";
    }

    public void OpenTab(int index)
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            if (i == index)
            {
                tabs[i].gameObject.SetActive(true);
            }
            else
            {
                tabs[i].gameObject.SetActive(false);
            }
        }
    }

    public void ProdBuySelected(int index)
    {
        for (int i = 0; i < prodBuys.Length; i++)
        {
            if (i == index)
            {
                prodBuys[i].color = Color.white;
            }
            else
            {
                prodBuys[i].color = Color.grey;
            }
        }
    }
}
