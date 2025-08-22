using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class AchvRowUI : MonoBehaviour
{
    public enum RowType { Producer, Recipe, Storage }
    public RowType type;
    public int index;

    public TextMeshProUGUI txtName;
    public TextMeshProUGUI txtStatus;
    public TextMeshProUGUI txtReward;
    public Button btnClaim;

    void Start()
    {
        btnClaim.onClick.AddListener(OnClaim);
        KimchiGame.Instance.OnAnyChanged += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        if (KimchiGame.Instance != null) KimchiGame.Instance.OnAnyChanged -= Refresh;
    }

    void Refresh()
    {
        var g = KimchiGame.Instance;

        switch (type)
        {
            case RowType.Producer:
                {
                    txtName.text = $"[Producer] {g.producers[index].name}";
                    int c = g.ProducerClaimableCount(index);
                    txtStatus.text = (c > 0) ? $"Claimable ¡¿{c}" : $"No claims (Lv {g.states[index].level})";
                    txtReward.text = "Reward: 10 coins ¡¿ claim";
                    btnClaim.interactable = c > 0;
                    break;
                }
            case RowType.Recipe:
                {
                    txtName.text = $"[Recipe] {g.recipeNames[index]}";
                    int c = g.RecipeClaimableCount(index);
                    txtStatus.text = (c > 0) ? $"Claimable ¡¿{c}" : $"No claims (Lv {g.recipeLv[index]}/10)";
                    txtReward.text = "Reward: 100 coins ¡¿ claim";
                    btnClaim.interactable = c > 0;
                    break;
                }
            case RowType.Storage:
                {
                    var s = g.storage[index];
                    txtName.text = $"[Storage] {s.name}";
                    if (s.claimed) txtStatus.text = "Claimed";
                    else if (s.unlocked) txtStatus.text = "Unlocked (Claimable)";
                    else txtStatus.text = "Locked";
                    txtReward.text = $"Reward: {s.coinReward:N0} coins";
                    btnClaim.interactable = g.StorageClaimAvailable(index);
                    break;
                }
        }
    }

    void OnClaim()
    {
        var g = KimchiGame.Instance;
        switch (type)
        {
            case RowType.Producer: g.ClaimProducerMilestones(index); break;
            case RowType.Recipe: g.ClaimRecipeMilestones(index); break;
            case RowType.Storage: g.ClaimStorageMilestone(index); break;
        }
        Refresh();
    }
}
