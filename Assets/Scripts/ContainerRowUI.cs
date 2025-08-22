using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ContainerRowUI : MonoBehaviour
{
    public int index;
    public Image imgIcon;
    [Header("Icon tint")]
    public Color iconColorNormal = Color.white;
    public Color iconColorDark = new Color(0.35f, 0.35f, 0.35f, 1f);

    public TextMeshProUGUI txtName, txtStatus, txtBuff, txtThreshold, txtContribution;
    public Button btnAction;
    public TextMeshProUGUI btnLabel;

    void Start()
    {
        var g = KimchiGame.Instance;
        if (imgIcon)
        {
            imgIcon.sprite = g.storage[index].icon;
            imgIcon.enabled = imgIcon.sprite != null;
            imgIcon.preserveAspect = true;
        }
        btnAction.onClick.AddListener(OnAction);
        g.OnAnyChanged += Refresh;
        Refresh();
    }

    void OnDestroy() { if (KimchiGame.Instance) KimchiGame.Instance.OnAnyChanged -= Refresh; }

    void OnAction() { KimchiGame.Instance.ClaimStorageMilestone(index); Refresh(); }

    void Refresh()
    {
        var g = KimchiGame.Instance;
        var s = g.storage[index];

        if (txtName) txtName.text = s.name;
        if (txtBuff) txtBuff.text = $"+{s.buffPct}% Total";
        if (txtThreshold) txtThreshold.text = $"Needs: {s.cpsThreshold}/s";

        int pct = g.StorageContributionPct(index);
        double share = g.ShareOfTotal(pct);
        if (txtContribution) txtContribution.text = s.unlocked ? $"+{pct}% to Total  ({share:0.#}% of total)" : "+0% (locked)";

        if (s.claimed)
        {
            if (txtStatus) txtStatus.text = "Claimed";
            if (btnLabel) btnLabel.text = "Claimed";
            if (btnAction) btnAction.interactable = false;
        }
        else if (s.unlocked)
        {
            if (txtStatus) txtStatus.text = "Unlocked";
            if (btnLabel) btnLabel.text = $"Claim\n{s.coinReward:N0} coins";
            if (btnAction) btnAction.interactable = true;
        }
        else
        {
            if (txtStatus) txtStatus.text = "Locked";
            if (btnLabel) btnLabel.text = "Locked";
            if (btnAction) btnAction.interactable = false;
        }

        // NEW: darken icon when locked (treat as "level 0")
        if (imgIcon) imgIcon.color = s.unlocked ? iconColorNormal : iconColorDark;
    }
}
