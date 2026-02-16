using Npgsql;

public static class Database
{
    static string conn =
        Environment.GetEnvironmentVariable("DATABASE_URL");

    public static async Task SaveUser(string username,string phone)
    {
        await using var con = new NpgsqlConnection(conn);
        await con.OpenAsync();

        var cmd = new NpgsqlCommand(
        "INSERT INTO users(username,phone) VALUES(@u,@p)", con);

        cmd.Parameters.AddWithValue("u", username ?? "");
        cmd.Parameters.AddWithValue("p", phone);

        await cmd.ExecuteNonQueryAsync();
    }
}


