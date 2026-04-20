public static class ValidacoesEntrada
{
    public static bool CpfTem11Digitos(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return false;

        var digitos = new string(cpf.Where(char.IsDigit).ToArray());
        return digitos.Length == 11;
    }

    public static bool NomeObrigatorio(string? nome) => !string.IsNullOrWhiteSpace(nome);

    public static bool EmailObrigatorio(string? email) => !string.IsNullOrWhiteSpace(email);

    public static bool DataEventoFutura(DateTime dataEvento) => dataEvento > DateTime.Now;

    public static bool CapacidadePositiva(int capacidadeTotal) => capacidadeTotal > 0;

    public static bool PrecoNaoNegativo(decimal preco) => preco >= 0;

    public static bool PercentualValido(decimal percentual) => percentual > 0 && percentual <= 100;

}
