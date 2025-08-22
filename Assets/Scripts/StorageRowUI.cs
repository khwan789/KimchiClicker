using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class StorageRowUI : MonoBehaviour
{
    public int index;
    public Image imgIcon;
    [Header("Icon tint")]
    public Color iconColorNormal = Color.white;
    public Color iconColorDark = new Color(0.35f, 0.35f, 0.35f, 1f);

    public TextMeshProUGUI txtName;
    public TextMeshProUGUI txtStatus;
    public TextMeshProUGUI txtBuff;        // "+100% Total"
    public TextMeshProUGUI txtThreshold;   // "Needs: 1C/s"
    public TextMeshProUGUI txtContribution;// "+100% to Total (x% of total)"

    public Button btnUnlock;
    public TextMeshProUGUI btnLabel;       // "Unlock" / "Purchased"

    void Start()
    {
        var g = KimchiGame.Instance;

        if (imgIcon)
        {
            var sIcon = g.storage[index].icon;
            imgIcon.sprite = sIcon;
            imgIcon.enabled = sIcon != null;
            imgIcon.preserveAspect = true;
        }

        btnUnlock.onClick.AddListener(OnUnlock);
        g.OnAnyChanged += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        if (KimchiGame.Instance != null) KimchiGame.Instance.OnAnyChanged -= Refresh;
    }

    void OnUnlock()
    {
        if (KimchiGame.Instance.TryUnlockStorage(index))
            Refresh();
    }

    void Refresh()
    {
        var g = KimchiGame.Instance;
        var s = g.storage[index];

        if (txtName) txtName.text = s.name;
        if (txtBuff) txtBuff.text = $"+{s.buffPct}% Total";

        // Show both needed and current CPS to make it obvious
        var cpsNow = g.CPS();
        if (txtThreshold) txtThreshold.text = $"Needs: {s.cpsThreshold}/s\nNow: {cpsNow}/s";

        // Contribution label
        int pct = g.StorageContributionPct(index);
        double share = g.ShareOfTotal(pct);
        if (txtContribution) txtContribution.text = s.unlocked ? $"+{pct}% to Total  ({share:0.#}% of total)" : "+0% (locked)";

        if (s.unlocked)
        {
            // Already purchased
            if (txtStatus) txtStatus.text = "Purchased";
            if (btnLabel) btnLabel.text = "Purchased";
            if (btnUnlock) btnUnlock.interactable = false;
        }
        else
        {
            // Not purchased: show the ¡°price¡± (threshold) on the button
            bool can = g.CanUnlockStorage(index);
            if (txtStatus) txtStatus.text = can ? "Ready to Unlock" : "Locked";

            if (btnLabel) btnLabel.text = $"Unlock\n{s.cpsThreshold}/s";  // << price shown here
            if (btnUnlock) btnUnlock.interactable = can;
        }

        // Tint icon
        if (imgIcon) imgIcon.color = s.unlocked ? iconColorNormal : iconColorDark;
    }
}
