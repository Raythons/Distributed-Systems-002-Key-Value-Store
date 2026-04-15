
// ============================================================
// Layer 1: RESP Parser — dumb, knows NOTHING about commands.
// Only job: turn raw RESP wire string into a clean RespToken[].
// Also here its like the compilers Parser  it parse the  incoming string to Lex it later via Lexer :) 
// ============================================================
public static class RespParser
{
    public static RespToken[]? Parse(string input)
    {
        var parts = input.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        // Must start with '*' 
        if (parts.Length == 0 || !parts[0].StartsWith("*"))
            return null;

        var tokens = new List<RespToken>();

        // Tokens come in pairs: "$length" then "value" 
        for (int i = 1; i + 1 < parts.Length; i += 2)
        {
            if (parts[i].StartsWith("$") &&
                int.TryParse(parts[i][1..], out int declaredLen))
            {
                string value = parts[i + 1];

                // Validate: declared length must match actual length
                if (value.Length != declaredLen)
                {
                    Console.WriteLine($"[WARN] RESP length mismatch: declared={declaredLen}, actual={value.Length}");
                }

                tokens.Add(new RespToken(declaredLen, value));
            }
        }

        return tokens.Count > 0 ? tokens.ToArray() : null;
    }
}
