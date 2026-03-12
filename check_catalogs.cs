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
            
            // Check Generos
            Console.WriteLine("CAT.GENEROS:");
            using (SqlCommand cmd = new SqlCommand("SELECT ID_GENERO, DESC_GENERO, ID_ESTADO FROM CAT.GENEROS", conn))
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    Console.WriteLine($"ID: {reader["ID_GENERO"]}, Desc: {reader["DESC_GENERO"]}, Estado: {reader["ID_ESTADO"]}");
                }
            }

            // Check Tipos Identificacion
            Console.WriteLine("\nCAT.TIPO_IDENTIFICACION:");
            using (SqlCommand cmd = new SqlCommand("SELECT ID_TIPO_IDENTIFICACION, DESC_TIPO, ID_ESTADO FROM CAT.TIPO_IDENTIFICACION", conn))
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    Console.WriteLine($"ID: {reader["ID_TIPO_IDENTIFICACION"]}, Desc: {reader["DESC_TIPO"]}, Estado: {reader["ID_ESTADO"]}");
                }
            }
        }
    }
}
