using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class ResourceWallet : MonoBehaviour
    {
        [SerializeField] int food = 600;
        [SerializeField] int wood = 350;
        public void Initialize(int startingFood, int startingWood) { food = Mathf.Max(0, startingFood); wood = Mathf.Max(0, startingWood); }
        public int Food => food;
        public int Wood => wood;
        public bool CanAfford(int foodCost, int woodCost) => foodCost >= 0 && woodCost >= 0 && food >= foodCost && wood >= woodCost;
        public bool TrySpend(int foodCost, int woodCost)
        {
            if (!CanAfford(foodCost, woodCost)) return false;
            food -= foodCost; wood -= woodCost; return true;
        }
        public void Refund(int foodAmount, int woodAmount)
        {
            if (foodAmount < 0 || woodAmount < 0) return;
            food += foodAmount; wood += woodAmount;
        }
    }
}
