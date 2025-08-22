using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class AchievementsPanelUI : MonoBehaviour
{
    [Header("Containers")]
    public Transform producersParent;
    public Transform recipesParent;
    public Transform storageParent;

    [Header("Prefabs")]
    public AchvRowUI rowPrefab;

    [Header("Top Summary")]
    public TextMeshProUGUI txtSummary;   // e.g., "Claimable: 1,230 coins (12 items)"
    public Button btnClaimAll;

    bool built = false;

    void Start()
    {
        BuildIfNeeded();
        KimchiGame.Instance.OnAnyChanged += RefreshSummary;
        btnClaimAll.onClick.AddListener(ClaimAll);
        RefreshSummary();
    }

    void OnDestroy()
    {
        if (KimchiGame.Instance != null) KimchiGame.Instance.OnAnyChanged -= RefreshSummary;
    }

    void BuildIfNeeded()
    {
        if (built) return;
        var g = KimchiGame.Instance;

        // Producers
        for (int i = 0; i < g.producers.Count; i++)
        {
            var row = Instantiate(rowPrefab, producersParent);
            row.type = AchvRowUI.RowType.Producer;
            row.index = i;
        }

        // Recipes
        for (int i = 0; i < g.recipeNames.Length; i++)
        {
            var row = Instantiate(rowPrefab, recipesParent);
            row.type = AchvRowUI.RowType.Recipe;
            row.index = i;
        }

        // Storage
        for (int i = 0; i < g.storage.Length; i++)
        {
            var row = Instantiate(rowPrefab, storageParent);
            row.type = AchvRowUI.RowType.Storage;
            row.index = i;
        }

        built = true;
    }

    void RefreshSummary()
    {
        var g = KimchiGame.Instance;
        var (coins, items) = g.PreviewClaimAllAchievements();
        if (txtSummary) txtSummary.text = $"Claimable: {coins:N0} coins ({items} items)";
        btnClaimAll.interactable = items > 0;
    }

    void ClaimAll()
    {
        var g = KimchiGame.Instance;
        g.ClaimAllAchievements();
        RefreshSummary();
    }
}
