using System.Data;


namespace SanctuaryMUD;

public static class ItemManager
{
    public static Item GetItem(int id)
    {
        string query = $"SELECT * FROM items WHERE id = {id};";
        DataTable dt = DBHelper.ExecuteQuery(query);

        if (dt.Rows.Count == 0)
        {
            return null;
        }

        DataRow row = dt.Rows[0];
        return new Item
        {
            ID = GetValueOrDefault<int>(row["id"]),
            Name = GetValueOrDefault<string>(row["name"]),
            Slot = GetValueOrDefault<string>(row["slot"]),
            SlotNumber = GetValueOrDefault<int>(row["slotNumber"]),
            Str = GetValueOrDefault<int>(row["str"]),
            Dex = GetValueOrDefault<int>(row["dex"]),
            Wis = GetValueOrDefault<int>(row["wis"]),
            Int = GetValueOrDefault<int>(row["int"]),
            Con = GetValueOrDefault<int>(row["con"]),
            Cha = GetValueOrDefault<int>(row["cha"]),
            Type = GetValueOrDefault<string>(row["type"]),
            Alias = GetValueOrDefault<string>(row["alias"]),
            IsFashion = (row["isFashion"] is bool) ? GetValueOrDefault<bool>(row["isFashion"]) : (GetValueOrDefault<int>(row["isFashion"]) == 1),
            Description = GetValueOrDefault<string>(row["description"]),
        };

    }
    private static T GetValueOrDefault<T>(object value)
    {
        if (DBNull.Value.Equals(value))
        {
            return default(T);
        }

        if (typeof(T) == typeof(int) && value is long)
        {
            return (T)(object)(int)(long)value;
        }
        else if (typeof(T) == typeof(int) && value is string)
        {
            if (int.TryParse((string)value, out int intValue))
            {
                return (T)(object)intValue;
            }
            else
            {
                return default(T);
            }
        }
        else
        {
            return (T)value;
        }
    }

    public static void AddItem(Item item)
    {
        string query =
            $@"INSERT INTO items (id, name, slot, slotNumber, str, dex, wis, int, con, cha, type, alias, isFashion, description) 
                        VALUES ({item.ID}, '{item.Name}', '{item.Slot}', {item.SlotNumber}, {item.Str}, {item.Dex}, {item.Wis}, {item.Int}, {item.Con}, {item.Cha}, '{item.Type}', '{item.Alias}', {Convert.ToInt32(item.IsFashion)}, '{item.Description}');";
        DBHelper.ExecuteNonQuery(query);
    }

    public static void UpdateItem(Item item)
    {
        string query = $@"UPDATE items SET 
                    name = '{item.Name}', 
                    slot = '{item.Slot}', 
                    slotNumber = {item.SlotNumber}, 
                    str = {item.Str}, 
                    dex = {item.Dex}, 
                    wis = {item.Wis}, 
                    int = {item.Int}, 
                    con = {item.Con}, 
                    cha = {item.Cha}, 
                    type = '{item.Type}', 
                    alias = '{item.Alias}', 
                    isFashion = {Convert.ToInt32(item.IsFashion)}, 
                    description = '{item.Description}' 
                    WHERE id = {item.ID};";
        DBHelper.ExecuteNonQuery(query);
    }
    
    public static Item GetItemByName(string name)
    {
        string query = $"SELECT * FROM items WHERE LOWER(name) = '{name}';";
        DataTable dt = DBHelper.ExecuteQuery(query);

        if (dt.Rows.Count == 0)
        {
            return null;
        }

        DataRow row = dt.Rows[0];
        return new Item
        {
            ID = Convert.ToInt32(row["id"]),
            Name = row["name"].ToString(),
            Slot = row["slot"].ToString(),
            SlotNumber = Convert.ToInt32(row["slotNumber"]),
            Str = Convert.ToInt32(row["str"]),
            Dex = Convert.ToInt32(row["dex"]),
            Wis = Convert.ToInt32(row["wis"]),
            Int = Convert.ToInt32(row["int"]),
            Con = Convert.ToInt32(row["con"]),
            Cha = Convert.ToInt32(row["cha"]),
            Type = row["type"].ToString(),
            Alias = row["alias"].ToString(),
            IsFashion = Convert.ToBoolean(row["isFashion"]),
            Description = row["description"].ToString()
        };
    }

}