
// A RESP bulk string: its declared length + its actual value
public record struct RespToken(int DeclaredLength, string Value);
