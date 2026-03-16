using System;
using Microsoft.Data.SqlClient;

namespace FixDatabase
{
    class Program
    {
        static void Main(string[] args)
        {
            string connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=MediTech;Trusted_Connection=True;TrustServerCertificate=True";
            string sql = "ALTER TABLE CLI.CITAS ALTER COLUMN ID_PACIENTE INT NULL;";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        command.ExecuteNonQuery();
                        Console.WriteLine("Schema updated successfully: ID_PACIENTE is now NULLABLE.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error updating schema: " + ex.Message);
                }
            }
        }
    }
}
