using InventoryControlSystem;
using OrderProcessingSystem;
using System;
using System.Threading.Tasks;
using TestingEngine;

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
            _service.RegisterItem(new Product
            {
                Id = 1,
                Name = "Test",
                Quantity = q,
                WeightPerUnit = (decimal)w
            });
            Verify.ThatEqual(exp, _service.GetTotalWeight());
        }

        [Case("Поиск по ID (Параметры)")]
        [CaseData(1)] // 4
        [CaseData(2)] // 5
        [CaseData(3)] // 6
        public void SearchByIdTest(int id)
        {
            _service.RegisterItem(new Product
            {
                Id = id,
                Name = "Product" + id,
                Quantity = 1,
                WeightPerUnit = 1.0m  // Добавлено значение по умолчанию
            });
            Verify.NotNull(_service.FindById(id));
        }

        [Case("Проверка полей товара", Order = -1)] // 7
        public void FieldsTest()
        {
            var i = new Product
            {
                Id = 777,
                Name = "Gold Product",
                Quantity = 7,
                WeightPerUnit = 0.5m  // Используем суффикс m для decimal
            };
            Verify.ContainsText("Gold", i.Name);
            Verify.IsType<Product>(i);
            Verify.GreaterThan(i.Quantity, 0);
        }

        [Case("Пустой склад", Order = -1)] // 8
        [Skip]
        public void EmptyTest()
        {
            Verify.ThatEqual(0, _service.TotalTypes);
            Verify.IsNull(_service.FindById(999));
        }

        [Case("Ошибка: пустой Name")] // 9
        public void ErrorNameTest() =>
            Verify.ExpectException<ArgumentException>(() =>
                _service.RegisterItem(new Product
                {
                    Id = 1,
                    Name = "",
                    Quantity = 1,
                    WeightPerUnit = 1.0m
                }));

        [Case("Ошибка: минус в кол-ве")] // 10
        public void ErrorQtyTest() =>
            Verify.ExpectException<InvalidOperationException>(() =>
                _service.RegisterItem(new Product
                {
                    Id = 1,
                    Name = "Test",
                    Quantity = -1,
                    WeightPerUnit = 1.0m
                }));

        [Case("Удаление товара")] // 11
        public void RemoveTest()
        {
            _service.RegisterItem(new Product
            {
                Id = 100,
                Name = "Delete Me",
                Quantity = 1,
                WeightPerUnit = 1.0m
            });
            _service.RemoveItem(100);
            Verify.ThatFalse(_service.TotalTypes > 0);
        }

        [Case("Обновление количества")] // 12
        public void UpdateTest()
        {
            _service.RegisterItem(new Product
            {
                Id = 200,
                Name = "Update Me",
                Quantity = 5,
                WeightPerUnit = 1.0m
            });
            _service.UpdateQuantity(200, 100);
            Verify.ThatEqual(100, _service.FindById(200).Quantity);
        }

        [Case("Асинхронная синхронизация")] // 13
        public async Task SyncTest()
        {
            _service.RegisterItem(new Product
            {
                Id = 300,
                Name = "Sync Me",
                Quantity = 1,
                WeightPerUnit = 1.0m
            });
            Verify.GreaterThan(await _service.SyncWithCloudAsync(), 0);
        }

        [Case("Фильтр веса")]
        public void FilterTest()
        {
            _service.RegisterItem(new Product
            {
                Id = 400,
                Name = "Heavy",
                WeightPerUnit = 200m
            });
            Verify.NotEqual(0, _service.GetItemsByWeight(150).Count);
        }

        [Case("Массовый ввод")] 
        public void MassAddTest()
        {
            for (int i = 0; i < 5; i++)
                _service.RegisterItem(new Product
                {
                    Id = 500 + i,
                    Name = "Item" + i,
                    Quantity = 1,
                    WeightPerUnit = 1.0m
                });
            Verify.ThatTrue(_service.TotalTypes == 5);
        }

        [Case("Пропущенный тест")] 
        [Skip]
        public void Skipped() { }

        [Case("Специальный провал (Demo)")] 
        public void IntentionalFail() => Verify.ThatEqual(1, 2);

        [Case("Критическая ошибка (Demo)")]
        public void CrashTest() { Product p = null; var x = p.Name; }

        [Cleanup]
        public void End() => _service = null;
    }
}
