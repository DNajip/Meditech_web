using System;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main()
    {
        string connectionString = "Server=(localdb)\\mssqllocaldb;Database=MediTech;Trusted_Connection=True;MultipleActiveResultSets=true";
        string sql = "ALTER TABLE INV.PRODUCTOS ADD CONSTRAINT CHK_PRODUCTO_STOCK_POSITIVO CHECK (STOCK >= 0);";

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            try
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    command.ExecuteNonQuery();
                    Console.WriteLine("Constraint CHK_PRODUCTO_STOCK_POSITIVO applied successfully.");
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 1779 || ex.Number == 2714) // Already exists
                {
                    Console.WriteLine("Constraint already exists.");
                }
                else
                {
                    Console.WriteLine("Error applying constraint: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("General Error: " + ex.Message);
            }
        }
    }
}
