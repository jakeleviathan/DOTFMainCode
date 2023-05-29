namespace SanctuaryMUD;

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;

public static class DBHelper
{
    private static string _connectionString = "Data Source=SanctuaryMUD.db";

    public static void ExecuteNonQuery(string commandText)
    {
        using (var connection = new SQLiteConnection(_connectionString))
        {
            connection.Open();

            using (var command = new SQLiteCommand(commandText, connection))
            {
                command.ExecuteNonQuery();
            }
        }
    }

    public static DataTable ExecuteQuery(string commandText)
    {
        DataTable dt = new DataTable();

        using (var connection = new SQLiteConnection(_connectionString))
        {
            connection.Open();

            using (var command = new SQLiteCommand(commandText, connection))
            using (SQLiteDataAdapter adapter = new SQLiteDataAdapter(command))
            {
                adapter.Fill(dt);
            }
        }

        return dt;
    }
}