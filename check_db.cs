using Microsoft.Data.SqlClient;
using System;

class Program
{
    static void Main()
    {
        string connString = "Server=(localdb)\\MSSQLLocalDB;Database=MediTech;Trusted_Connection=True;TrustServerCertificate=True";
        using (SqlConnection conn = new SqlConnection(connString))
        {
            conn.Open();
            string query = "SELECT Username, ID_ESTADO, ID_ROL FROM ADM.USUARIOS";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    Console.WriteLine("USUARIOS:");
                    while (reader.Read())
                    {
                        Console.WriteLine($"Username: {reader["Username"]}, Estado: {reader["ID_ESTADO"]}, Rol: {reader["ID_ROL"]}");
                    }
                }
            }
        }
    }
}
