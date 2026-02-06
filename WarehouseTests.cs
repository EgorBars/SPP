using TestingEngine;
using InventoryControlSystem;
using System;
using System.Threading.Tasks;

namespace InventoryTests
{
    [Suite]
    public class WarehouseTests
    {
        private WarehouseManager _service;

        [Startup]
        public void Init() => _service = new WarehouseManager();

        [Case("Расчет веса (Параметры)")]
        [CaseData(10, 2.5, 25.0)] // 1
        [CaseData(5, 4.0, 20.0)]  // 2
        [CaseData(1, 100.0, 100.0)] // 3
        public void WeightTest(int q, double w, double exp)
        {
            _service.ClearInventory();
            _service.RegisterItem(new StockItem { Sku = "T", Quantity = q, WeightPerUnit = w });
            Verify.ThatEqual(exp, _service.GetTotalWeight());
        }

        [Case("Поиск SKU (Параметры)")]
        [CaseData("A1")] // 4
        [CaseData("B2")] // 5
        [CaseData("C3")] // 6
        public void SearchSkuTest(string code)
        {
            _service.RegisterItem(new StockItem { Sku = code, Quantity = 1 });
            Verify.NotNull(_service.FindBySku(code));
        }

        [Case("Проверка полей товара")] // 7
        public void FieldsTest()
        {
            var i = new StockItem { Sku = "GOLD-777", Quantity = 7, WeightPerUnit = 0.5 };
            Verify.ContainsText("GOLD", i.Sku);
            Verify.IsType<StockItem>(i);
            Verify.GreaterThan(i.Quantity, 0);
        }

        [Case("Пустой склад")] // 8
        public void EmptyTest()
        {
            Verify.ThatEqual(0, _service.TotalTypes);
            Verify.IsNull(_service.FindBySku("NONE"));
        }

        [Case("Ошибка: пустой SKU")] // 9
        public void ErrorSkuTest() =>
            Verify.ExpectException<ArgumentException>(() => _service.RegisterItem(new StockItem { Sku = "" }));

        [Case("Ошибка: минус в кол-ве")] // 10
        public void ErrorQtyTest() =>
            Verify.ExpectException<InvalidOperationException>(() => _service.RegisterItem(new StockItem { Sku = "X", Quantity = -1 }));

        [Case("Удаление товара")] // 11
        public void RemoveTest()
        {
            _service.RegisterItem(new StockItem { Sku = "DEL", Quantity = 1 });
            _service.RemoveItem("DEL");
            Verify.ThatFalse(_service.TotalTypes > 0);
        }

        [Case("Обновление количества")] // 12
        public void UpdateTest()
        {
            _service.RegisterItem(new StockItem { Sku = "U", Quantity = 5 });
            _service.UpdateQuantity("U", 100);
            Verify.ThatEqual(100, _service.FindBySku("U").Quantity);
        }

        [Case("Асинхронная синхронизация")] // 13
        public async Task SyncTest()
        {
            _service.RegisterItem(new StockItem { Sku = "S", Quantity = 1 });
            Verify.GreaterThan(await _service.SyncWithCloudAsync(), 0);
        }

        [Case("Фильтр веса")] // 14
        public void FilterTest()
        {
            _service.RegisterItem(new StockItem { Sku = "H", WeightPerUnit = 200 });
            Verify.NotEqual(0, _service.GetItemsByWeight(150).Count);
        }

        [Case("Массовый ввод")] // 15
        public void MassAddTest()
        {
            for (int i = 0; i < 5; i++) _service.RegisterItem(new StockItem { Sku = "I" + i, Quantity = 1 });
            Verify.ThatTrue(_service.TotalTypes == 5);
        }

        [Case("Пропущенный тест")] // 16
        [Skip]
        public void Skipped() { }

        [Case("Специальный провал (Demo)")] // 17
        public void IntentionalFail() => Verify.ThatEqual(1, 2);

        [Case("Критическая ошибка (Demo)")] // 18
        public void CrashTest() { StockItem s = null; var x = s.Sku; }

        [Cleanup]
        public void End() => _service = null;
    }
}