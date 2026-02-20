using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OrderProcessingSystem;

namespace InventoryControlSystem
{
    public class WarehouseManager
    {
        private List<Product> _items = new List<Product>();

        public void RegisterItem(Product item)
        {
            if (item.Id <= 0) throw new ArgumentException("Invalid product ID");
            if (string.IsNullOrWhiteSpace(item.Name)) throw new ArgumentException("Product name is empty");
            _items.Add(item);
        }

        public double GetTotalWeight() => _items.Sum(i => (double)i.WeightPerUnit);

        public Product FindById(int id) => _items.FirstOrDefault(x => x.Id == id);

        public void RemoveItem(int id) =>
    _items.RemoveAll(x => x.Id == id);

        public void UpdateQuantity(int id, int newQty)
        {
            var item = FindById(id);
            if (item != null) item.Quantity = newQty;
        }

        public List<Product> GetItemsByWeight(double minWeight) =>
            _items.Where(i => (double)i.WeightPerUnit >= minWeight).ToList();

        public async Task<int> SyncWithCloudAsync()
        {
            await Task.Delay(20);
            return _items.Count;
        }

        public void ClearInventory() => _items.Clear();
        public int TotalTypes => _items.Count;
    }
}
