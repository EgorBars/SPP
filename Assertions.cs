using System;

namespace TestingEngine
{
    public static class Verify
    {
        // 1. На равенство
        public static void ThatEqual(object exp, object act)
        {
            if (!Equals(exp, act)) throw new EngineException($"Ждали {exp}, получили {act}");
        }

        // 2. На неравенство
        public static void NotEqual(object v1, object v2)
        {
            if (Equals(v1, v2)) throw new EngineException($"Значения {v1} и {v2} равны, а не должны");
        }

        // 3. На истину
        public static void ThatTrue(bool cond)
        {
            if (!cond) throw new EngineException("Условие ложно");
        }

        // 4. На ложь
        public static void ThatFalse(bool cond)
        {
            if (cond) throw new EngineException("Условие истинно");
        }

        // 5. Наличие объекта (не null)
        public static void NotNull(object o)
        {
            if (o == null) throw new EngineException("Объект равен null");
        }

        // 6. Отсутствие объекта (null)
        public static void IsNull(object o)
        {
            if (o != null) throw new EngineException("Объект не равен null");
        }

        // 7. Сравнение (больше чем)
        public static void GreaterThan(double v, double lim)
        {
            if (v <= lim) throw new EngineException($"{v} не больше {lim}");
        }

        // 8. Содержание текста
        public static void ContainsText(string p, string f)
        {
            if (f == null || !f.Contains(p)) throw new EngineException($"Текст '{f}' не содержит '{p}'");
        }

        // 9. Проверка типа
        public static void IsType<T>(object o)
        {
            if (!(o is T)) throw new EngineException($"Тип {o?.GetType().Name} не совпадает с {typeof(T).Name}");
        }

        // 10. Проверка на ожидаемое исключение
        public static void ExpectException<T>(Action a) where T : Exception
        {
            try { a(); }
            catch (T) { return; }
            catch (Exception e) { throw new EngineException($"Ждали {typeof(T).Name}, вылетело {e.GetType().Name}"); }
            throw new EngineException($"Исключение {typeof(T).Name} не вызвано");
        }
    }
}
