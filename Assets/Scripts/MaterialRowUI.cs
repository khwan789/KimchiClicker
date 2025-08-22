using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class MaterialRowUI : MonoBehaviour
{
    public int index; // 0..4
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
        if (txtName) txtName.text = g.materialNames[index];
        if (imgIcon)
        {
            var s = (g.materialIcons != null && index < g.materialIcons.Length) ? g.materialIcons[index] : null;
            imgIcon.sprite = s; imgIcon.enabled = s != null; imgIcon.preserveAspect = true;
        }
        btnUpgrade.onClick.AddListener(() => g.TryUpgradeMaterial(index));
        g.OnAnyChanged += Refresh;
        Refresh();
    }

    void OnDestroy() { if (KimchiGame.Instance) KimchiGame.Instance.OnAnyChanged -= Refresh; }

    void Refresh()
    {
        var g = KimchiGame.Instance;
        int lv = g.materialLv[index];
        long cost = g.materialCost[index];

        if (txtLevel) txtLevel.text = $"Lv {lv}";
        if (btnLabel) btnLabel.text = $"Upgrade\n{cost:N0} coins";
        int pct = g.MaterialContributionPct(index);
        double share = g.ShareOfTotal(pct);
        if (txtContribution) txtContribution.text = $"+{pct}% to Total  ({share:0.#}% of total)";

        if (btnUpgrade) btnUpgrade.interactable = g.coins >= cost;

        // NEW: darken icon at Lv 0
        if (imgIcon) imgIcon.color = (lv > 0) ? iconColorNormal : iconColorDark;
    }
}
