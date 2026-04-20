CREATE TABLE Usuarios(
    Cpf VARCHAR(11) PRIMARY KEY,
    Nome VARCHAR(100) NOT NULL,
    Email VARCHAR(150) NOT NULL,
    SenhaHash VARCHAR(255) NOT NULL DEFAULT '',
    DataCriacao DATETIME NOT NULL DEFAULT GETDATE(),
    IsAdmin BIT NOT NULL DEFAULT 0
);

CREATE TABLE Eventos(
    Id INT PRIMARY KEY IDENTITY(1,1),
    Nome VARCHAR(100) NOT NULL,
    CapacidadeTotal INT NOT NULL,
    DataEvento DATETIME NOT NULL,
    PrecoPadrao DECIMAL(10,2) NOT NULL,
    BannerUrl VARCHAR(500) NULL,
    GaleriaTexto TEXT NULL,
    Descricao TEXT NULL,
    LocalNome VARCHAR(200) NULL,
    LocalCidade VARCHAR(100) NULL,
    TipoEvento VARCHAR(20) NULL CHECK (TipoEvento IN ('estadio', 'teatro', 'show', 'outro')),
    MapaAssentosJson TEXT NULL,
    TaxaFixa DECIMAL(10,2) NOT NULL DEFAULT 5.00,
    Status VARCHAR(20) NOT NULL DEFAULT 'ativo' CHECK (Status IN ('ativo', 'cancelado', 'encerrado'))
);

CREATE TABLE Setores(
    Id INT PRIMARY KEY IDENTITY(1,1),
    EventoId INT NOT NULL,
    Nome VARCHAR(50) NOT NULL,
    Preco DECIMAL(10,2) NOT NULL,
    Cor VARCHAR(7) NULL,
    Capacidade INT NULL,
    Ordem INT NOT NULL DEFAULT 0,
    FOREIGN KEY (EventoId) REFERENCES Eventos(Id) ON DELETE CASCADE
);

CREATE TABLE Cupons(
    Codigo VARCHAR(50) PRIMARY KEY,
    PorcentagemDesconto DECIMAL(5,2) NOT NULL,
    ValorMinimoRegra DECIMAL(10,2) NOT NULL,
    DataExpiracao DATETIME NULL
);

CREATE TABLE Assentos(
    Id INT PRIMARY KEY IDENTITY(1,1),
    EventoId INT NOT NULL,
    Fila VARCHAR(10) NOT NULL,
    Numero VARCHAR(10) NOT NULL,
    Tipo VARCHAR(20) NOT NULL DEFAULT 'regular' CHECK (Tipo IN ('regular', 'vip', 'premium')),
    PrecoAdicional DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    Status VARCHAR(20) NOT NULL DEFAULT 'disponivel' CHECK (Status IN ('disponivel', 'reservado', 'ocupado', 'inativo')),
    LockedUntil DATETIME NULL,
    LockedByCpf VARCHAR(11) NULL,
    FOREIGN KEY (EventoId) REFERENCES Eventos(Id) ON DELETE CASCADE,
    FOREIGN KEY (LockedByCpf) REFERENCES Usuarios(Cpf)
);

CREATE TABLE Reservas(
    Id INT PRIMARY KEY IDENTITY(1,1),
    UsuarioCpf VARCHAR(11) NOT NULL,
    EventoId INT NOT NULL,
    CupomUtilizado VARCHAR(50) NULL,
    ValorFinalPago DECIMAL(10,2) NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'pendente' CHECK (Status IN ('pendente', 'confirmada', 'cancelada', 'estornada')),
    DataReserva DATETIME NOT NULL DEFAULT GETDATE(),
    PagamentoId INT NULL,
    FOREIGN KEY (UsuarioCpf) REFERENCES Usuarios(Cpf),
    FOREIGN KEY (EventoId) REFERENCES Eventos(Id),
    FOREIGN KEY (CupomUtilizado) REFERENCES Cupons(Codigo)
);

CREATE TABLE Tickets(
    Id INT PRIMARY KEY IDENTITY(1,1),
    ReservaId INT NOT NULL,
    AssentoId INT NOT NULL,
    TipoIngresso VARCHAR(30) NOT NULL DEFAULT 'inteira' CHECK (TipoIngresso IN ('inteira', 'meia_estudante', 'meia_idoso', 'promocional')),
    PrecoPago DECIMAL(10,2) NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'valido' CHECK (Status IN ('valido', 'cancelado', 'utilizado')),
    FOREIGN KEY (ReservaId) REFERENCES Reservas(Id) ON DELETE CASCADE,
    FOREIGN KEY (AssentoId) REFERENCES Assentos(Id)
);

CREATE TABLE Pagamentos(
    Id INT PRIMARY KEY IDENTITY(1,1),
    ReservaId INT NOT NULL,
    Metodo VARCHAR(20) NOT NULL CHECK (Metodo IN ('pix', 'cartao_credito', 'cartao_debito')),
    ValorTotal DECIMAL(10,2) NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'pendente' CHECK (Status IN ('pendente', 'aprovado', 'recusado', 'estornado')),
    TransacaoId VARCHAR(100) NULL,
    Parcelas INT NULL,
    DataPagamento DATETIME NULL,
    DataAtualizacao DATETIME NOT NULL DEFAULT GETDATE(),
    FOREIGN KEY (ReservaId) REFERENCES Reservas(Id)
);

CREATE TABLE FilaEvento(
    Id INT PRIMARY KEY IDENTITY(1,1),
    EventoId INT NOT NULL,
    UsuarioCpf VARCHAR(11) NOT NULL,
    Posicao INT NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'espera' CHECK (Status IN ('espera', 'processando', 'concluido', 'cancelado')),
    DataEntrada DATETIME NOT NULL DEFAULT GETDATE(),
    TempoEstimado INT NULL, -- em segundos
    FOREIGN KEY (EventoId) REFERENCES Eventos(Id),
    FOREIGN KEY (UsuarioCpf) REFERENCES Usuarios(Cpf)
);

CREATE INDEX IX_Assentos_EventoId ON Assentos(EventoId);
CREATE INDEX IX_Assentos_Status ON Assentos(Status);
CREATE INDEX IX_Assentos_LockedUntil ON Assentos(LockedUntil);
CREATE INDEX IX_Reservas_UsuarioCpf ON Reservas(UsuarioCpf);
CREATE INDEX IX_Reservas_EventoId ON Reservas(EventoId);
CREATE INDEX IX_Tickets_ReservaId ON Tickets(ReservaId);
CREATE INDEX IX_Tickets_AssentoId ON Tickets(AssentoId);
CREATE INDEX IX_Pagamentos_ReservaId ON Pagamentos(ReservaId);
CREATE INDEX IX_FilaEvento_EventoId ON FilaEvento(EventoId);
CREATE INDEX IX_FilaEvento_UsuarioCpf ON FilaEvento(UsuarioCpf);
CREATE INDEX IX_FilaEvento_Status ON FilaEvento(Status);

-- Insert default admin user (password: "admin123" hashed with BCrypt)
INSERT INTO Usuarios (Cpf, Nome, Email, SenhaHash, IsAdmin) VALUES
('00000000000', 'Administrador', 'admin@ticketprime.com', '$2a$11$K7Z5Y5Q5Z5Y5Q5Z5Y5Q5Z5Y5Q5Z5Y5Q5Z5Y5Q5Z5Y5Q5Z5Y5Q5Z5Y', 1);

-- Insert sample event
INSERT INTO Eventos (Nome, CapacidadeTotal, DataEvento, PrecoPadrao, LocalNome, LocalCidade, TipoEvento) VALUES
('Show do Metallica', 50000, DATEADD(day, 30, GETDATE()), 350.00, 'Allianz Parque', 'São Paulo', 'estadio'),
('Peça Hamlet', 200, DATEADD(day, 15, GETDATE()), 80.00, 'Teatro Municipal', 'Rio de Janeiro', 'teatro');

-- Insert sample coupons
INSERT INTO Cupons (Codigo, PorcentagemDesconto, ValorMinimoRegra) VALUES
('PRIME10', 10.00, 100.00),
('BLACK20', 20.00, 200.00);