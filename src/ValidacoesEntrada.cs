using System.Net.Mail;

public static class ValidacoesEntrada
{
    public const int TamanhoMinimoSenha = 8;
    public const int TamanhoCodigoVerificacao = 6;

    private static readonly string[] TiposEventoPermitidos = ["estadio", "teatro", "show", "outro"];
    private static readonly string[] DominiosConhecidos = ["gmail.com", "hotmail.com", "outlook.com", "live.com"];
    private static readonly Dictionary<string, string> DominiosComunsCorrigidos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gnail.com"] = "gmail.com",
        ["gmai.com"] = "gmail.com",
        ["gmial.com"] = "gmail.com",
        ["gmail.con"] = "gmail.com",
        ["hotnail.com"] = "hotmail.com",
        ["hotmai.com"] = "hotmail.com",
        ["hotmail.con"] = "hotmail.com",
        ["outlok.com"] = "outlook.com",
        ["outlook.con"] = "outlook.com",
        ["otlook.com"] = "outlook.com",
    };

    public static bool CpfTem11Digitos(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return false;

        var digitos = new string(cpf.Where(char.IsDigit).ToArray());
        return digitos.Length == 11;
    }

    public static bool NomeObrigatorio(string? nome) => !string.IsNullOrWhiteSpace(nome);

    public static bool NomeCompletoValido(string? nome) => ObterErroNomeCompleto(nome) is null;

    public static bool EmailObrigatorio(string? email) => !string.IsNullOrWhiteSpace(email);

    public static bool CpfValido(string? cpf) => ObterErroCpf(cpf) is null;

    public static string? ObterErroCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return "CPF e obrigatorio.";

        var digitos = new string(cpf.Where(char.IsDigit).ToArray());
        if (digitos.Length != 11)
            return "CPF deve conter 11 digitos.";

        if (digitos.Distinct().Count() == 1)
            return "CPF nao pode ser uma sequencia repetida.";

        if (!DigitosCpfConferem(digitos))
            return "CPF invalido.";

        return null;
    }

    public static string? ObterErroNomeCompleto(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return "Nome completo e obrigatorio.";

        var partes = nome.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length < 2)
            return "Informe nome e sobrenome.";

        if (partes[0].Length < 2 || partes[^1].Length < 2)
            return "Nome e sobrenome devem ter pelo menos 2 caracteres.";

        return null;
    }

    public static bool EmailValido(string? email)
    {
        return ObterErroEmail(email) is null;
    }

    public static string? ObterErroEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "E-mail e obrigatorio.";

        try
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var address = new MailAddress(normalizedEmail);
            if (!string.Equals(address.Address, normalizedEmail, StringComparison.OrdinalIgnoreCase))
                return "E-mail invalido.";

            var partes = normalizedEmail.Split('@', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (partes.Length != 2)
                return "E-mail invalido.";

            var dominio = partes[1];
            if (!dominio.Contains('.'))
                return "Dominio de e-mail invalido.";

            var sugestao = ObterSugestaoDominio(dominio);
            if (sugestao is not null)
                return $"Dominio de e-mail parece incorreto. Verifique se o correto e {sugestao}.";

            return null;
        }
        catch (FormatException)
        {
            return "E-mail invalido.";
        }
    }

    public static bool DataEventoFutura(DateTime dataEvento)
    {
        var utcDataEvento = dataEvento.Kind switch
        {
            DateTimeKind.Utc => dataEvento,
            DateTimeKind.Local => dataEvento.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dataEvento, DateTimeKind.Local).ToUniversalTime(),
        };

        return utcDataEvento > DateTime.UtcNow;
    }

    public static bool CapacidadePositiva(int capacidadeTotal) => capacidadeTotal > 0;

    public static bool PrecoNaoNegativo(decimal preco) => preco >= 0;

    public static bool PrecoPositivo(decimal preco) => preco > 0;

    public static bool PercentualValido(decimal percentual) => percentual > 0 && percentual <= 100;

    public static bool SenhaAtendeMinimo(string? senha) => !string.IsNullOrWhiteSpace(senha) && senha.Length >= TamanhoMinimoSenha;

    public static bool SenhaForteValida(string? senha) => ListarErrosSenha(senha).Count == 0;

    public static IReadOnlyList<string> ListarErrosSenha(string? senha)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(senha))
        {
            erros.Add("Senha e obrigatoria.");
            return erros;
        }

        if (senha.Length < TamanhoMinimoSenha)
            erros.Add($"Senha deve ter pelo menos {TamanhoMinimoSenha} caracteres.");

        if (!senha.Any(char.IsUpper))
            erros.Add("Senha deve conter ao menos uma letra maiuscula.");

        if (!senha.Any(char.IsLower))
            erros.Add("Senha deve conter ao menos uma letra minuscula.");

        var digitos = senha.Where(char.IsDigit).ToArray();
        if (digitos.Length == 0)
        {
            erros.Add("Senha deve conter ao menos um numero.");
        }
        else
        {
            if (digitos.Distinct().Count() < 2)
                erros.Add("Senha deve conter pelo menos dois numeros diferentes.");

            if (digitos.GroupBy(digito => digito).Any(grupo => grupo.Count() > 3))
                erros.Add("O mesmo numero nao pode aparecer mais de 3 vezes na senha.");
        }

        if (!senha.Any(caractere => !char.IsLetterOrDigit(caractere)))
            erros.Add("Senha deve conter ao menos um caractere especial.");

        return erros;
    }

    public static bool CodigoVerificacaoValido(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return false;

        return codigo.Length == TamanhoCodigoVerificacao && codigo.All(char.IsDigit);
    }

    public static bool TipoEventoValido(string? tipoEvento)
    {
        if (string.IsNullOrWhiteSpace(tipoEvento))
            return true;

        return TiposEventoPermitidos.Contains(tipoEvento.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static bool DigitosCpfConferem(string cpf)
    {
        var somaPrimeiroDigito = 0;
        for (var index = 0; index < 9; index++)
            somaPrimeiroDigito += (cpf[index] - '0') * (10 - index);

        var restoPrimeiroDigito = somaPrimeiroDigito % 11;
        var primeiroDigito = restoPrimeiroDigito < 2 ? 0 : 11 - restoPrimeiroDigito;
        if (cpf[9] - '0' != primeiroDigito)
            return false;

        var somaSegundoDigito = 0;
        for (var index = 0; index < 10; index++)
            somaSegundoDigito += (cpf[index] - '0') * (11 - index);

        var restoSegundoDigito = somaSegundoDigito % 11;
        var segundoDigito = restoSegundoDigito < 2 ? 0 : 11 - restoSegundoDigito;
        return cpf[10] - '0' == segundoDigito;
    }

    private static string? ObterSugestaoDominio(string dominio)
    {
        if (DominiosConhecidos.Contains(dominio, StringComparer.OrdinalIgnoreCase))
            return null;

        if (DominiosComunsCorrigidos.TryGetValue(dominio, out var dominioCorrigido))
            return dominioCorrigido;

        foreach (var dominioConhecido in DominiosConhecidos)
        {
            if (CalcularDistanciaLevenshtein(dominio, dominioConhecido) <= 2)
                return dominioConhecido;
        }

        return null;
    }

    private static int CalcularDistanciaLevenshtein(string origem, string destino)
    {
        var matriz = new int[origem.Length + 1, destino.Length + 1];

        for (var i = 0; i <= origem.Length; i++)
            matriz[i, 0] = i;

        for (var j = 0; j <= destino.Length; j++)
            matriz[0, j] = j;

        for (var i = 1; i <= origem.Length; i++)
        {
            for (var j = 1; j <= destino.Length; j++)
            {
                var custo = origem[i - 1] == destino[j - 1] ? 0 : 1;
                matriz[i, j] = Math.Min(
                    Math.Min(matriz[i - 1, j] + 1, matriz[i, j - 1] + 1),
                    matriz[i - 1, j - 1] + custo);
            }
        }

        return matriz[origem.Length, destino.Length];
    }

}
