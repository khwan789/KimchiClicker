using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class RecipeRowUI : MonoBehaviour
{
    public int index;
    public Image imgIcon;
    [Header("Icon tint")]
    public Color iconColorNormal = Color.white;
    public Color iconColorDark = new Color(0.35f, 0.35f, 0.35f, 1f);

    public TextMeshProUGUI txtName, txtLevel, txtContribution;
    public Button btnUpgrade;
    public TextMeshProUGUI btnLabel;

    void Start()
    {
        var g = KimchiGame.Instance;
        if (txtName) txtName.text = g.recipeNames[index];
        if (imgIcon)
        {
            var s = (g.recipeIcons != null && index < g.recipeIcons.Length) ? g.recipeIcons[index] : null;
            imgIcon.sprite = s; imgIcon.enabled = s != null; imgIcon.preserveAspect = true;
        }
        btnUpgrade.onClick.AddListener(() => g.TryUpgradeRecipe(index));
        g.OnAnyChanged += Refresh;
        Refresh();
    }

    void OnDestroy() { if (KimchiGame.Instance) KimchiGame.Instance.OnAnyChanged -= Refresh; }

    void Refresh()
    {
        var g = KimchiGame.Instance;
        int lv = g.recipeLv[index];
        long cost = g.recipeCost[index];

        if (txtLevel) txtLevel.text = $"Lv {lv}/10";
        int pct = g.RecipeContributionPct(index);
        double share = g.ShareOfTotal(pct);
        if (txtContribution) txtContribution.text = $"+{pct}% to Total  ({share:0.#}% of total)";

        if (lv >= 10)
        {
            if (btnLabel) btnLabel.text = "MAX";
            if (btnUpgrade) btnUpgrade.interactable = false;
        }
        else
        {
            if (btnLabel) btnLabel.text = $"Upgrade\n{cost:N0} keys";
            if (btnUpgrade) btnUpgrade.interactable = g.goldKeys >= cost;
        }

        // NEW: darken icon at Lv 0
        if (imgIcon) imgIcon.color = (lv > 0) ? iconColorNormal : iconColorDark;
    }
}
