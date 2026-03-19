CREATE TABLE Usuarios(
    CPF CHAR(11) PRIMARY KEY,
    Nome VARCHAR(100) NOT NULL,
    Email VARCHAR(150) NOT NULL
);

CREATE TABLE Eventos(
    Id INT PRIMARY KEY IDENTITY(1,1),
    Nome VARCHAR(100) NOT NULL,
    CapacidadeTotal INT NOT NULL,
    DataEvento DATETIME NOT NULL,
    PrecoPadrao DECIMAL(10,2) NOT NULL
);

CREATE TABLE Cupons(
    Codigo VARCHAR(50) PRIMARY KEY,
    PorcentagemDesconto DECIMAL(5,2) NOT NULL,
    ValorMinimo DECIMAL (10,2) NOT NULL
);

CREATE TABLE Reservas(
    Id INT PRIMARY KEY IDENTITY(1,1),
    UsuarioCPF CHAR(11) NOT NULL,
    EventoID INT NOT NULL,
    CupomUtilizado VARCHAR(50),
    ValorFinalPago DECIMAL(10,2) NOT NULL,

    CONSTRAINT FK_Reservas_Usuarios
        FOREIGN KEY (UsuarioCPF) REFERENCES Usuarios(CPF),

    CONSTRAINT FK_Reservas_Eventos
        FOREIGN KEY (EventoId) REFERENCES Eventos(Id),

    CONSTRAINT FK_Reservas_Cupons
        FOREIGN KEY (CupomUtilizado) REFERENCES Cupons(Codigo)
);