using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryControlSystem
{
    public class StockItem
    {
        public string Sku { get; set; }
        public int Quantity { get; set; }
        public double WeightPerUnit { get; set; }
    }

    public class WarehouseManager
    {
        private List<StockItem> _items = new List<StockItem>();

        public void RegisterItem(StockItem item)
        {
            if (string.IsNullOrWhiteSpace(item.Sku)) throw new ArgumentException("SKU is empty");
            if (item.Quantity < 0) throw new InvalidOperationException("Negative quantity");
            _items.Add(item);
        }

        public double GetTotalWeight() => _items.Sum(i => i.Quantity * i.WeightPerUnit);

        public StockItem FindBySku(string sku) => _items.FirstOrDefault(x => x.Sku == sku);

        public void RemoveItem(string sku) => _items.RemoveAll(x => x.Sku == sku);

        public void UpdateQuantity(string sku, int newQty)
        {
            var item = FindBySku(sku);
            if (item != null) item.Quantity = newQty;
        }

        public List<StockItem> GetItemsByWeight(double minWeight) =>
            _items.Where(i => i.WeightPerUnit >= minWeight).ToList();

        public async Task<int> SyncWithCloudAsync()
        {
            await Task.Delay(20);
            return _items.Count;
        }

        public void ClearInventory() => _items.Clear();
        public int TotalTypes => _items.Count;
    }
}