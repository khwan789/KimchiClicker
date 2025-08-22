using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ProducerRowUI : MonoBehaviour
{
    public int index;
    public Image imgIcon; // <-- set in Inspector
    [Header("Icon tint")]
    public Color iconColorNormal = Color.white;
    public Color iconColorDark = new Color(0.35f, 0.35f, 0.35f, 1f);

    public TextMeshProUGUI txtName, txtLevel, txtCps;
    public Button btnBuy;
    public TextMeshProUGUI btnBuyLabel;

    HoldToRepeat _hold;

    void Start()
    {
        var g = KimchiGame.Instance;
        if (txtName) txtName.text = g.producers[index].name;
        if (imgIcon)
        {
            imgIcon.sprite = g.producers[index].icon;
            imgIcon.enabled = imgIcon.sprite != null;
            imgIcon.preserveAspect = true;
        }

        // Ensure HoldToRepeat is present and hook the step
        _hold = btnBuy.GetComponent<HoldToRepeat>();
        if (_hold == null) _hold = btnBuy.gameObject.AddComponent<HoldToRepeat>();
        _hold.onStep.RemoveAllListeners();               // avoid duplicate bindings
        _hold.onStep.AddListener(BuyStep);               // call our method per step

        g.OnAnyChanged += Refresh;
        Refresh();
    }

    void OnDestroy() { if (KimchiGame.Instance) KimchiGame.Instance.OnAnyChanged -= Refresh; }

    void BuyStep()
    {
        var g = KimchiGame.Instance;

        // Optional: stop repeating if you can¡¯t afford the current batch
        var cost = g.PreviewCost(index);
        if (!g.CanAfford(cost))
        {
            _hold?.CancelHold();
            return;
        }

        g.TryBuyProducer(index); // buys using current buyAmount (1/10/100)
    }

    void Refresh()
    {
        var g = KimchiGame.Instance;
        var cost = g.PreviewCost(index);
        var cps = g.ProducerCPS(index);
        var delta = g.ProducerCPSDiffForPurchase(index, g.buyAmount);

        if (txtLevel) txtLevel.text = $"Lv {g.states[index].level}";
        if (txtCps) txtCps.text = $"{cps}/s";
        if (btnBuyLabel) btnBuyLabel.text = $"{cost}";
        if (btnBuy) btnBuy.interactable = g.CanAfford(cost);

        // NEW: darken icon at Lv 0
        if (imgIcon) imgIcon.color = (g.states[index].level > 0) ? iconColorNormal : iconColorDark;
    }
}
