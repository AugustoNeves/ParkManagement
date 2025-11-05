# Park Management System

Sistema de gerenciamento de estacionamento com arquitetura orientada a eventos, desenvolvido para demonstrar competências em ASP.NET Core, Entity Framework e padrões de design modernos. O sistema gerencia entrada/saída de veículos, alocação dinâmica de vagas, precificação inteligente baseada em ocupação e cálculo de receita em tempo real.

## Arquitetura

O sistema é composto por duas APIs independentes usando **ASP.NET Core 9.0 Minimal APIs**:

### 1. Garage.Simulator.Api (Porta 5000)

- Simula serviço externo de configuração do estacionamento
- Endpoint: `GET /garage`
- Retorna setores (A, B, C) com preços e capacidades
- Gera 15 vagas por setor (total de 45 vagas) com coordenadas GPS
- **Swagger UI**: http://localhost:5000

### 2. Parking.Management.Api (Porta 5041)

- API principal de gerenciamento
- Processa eventos via webhook: `POST /webhook`
- Calcula receita: `GET /revenue?sector={sector}&date={YYYY-MM-DD}`
- Armazena sessões e calcula tarifas com precificação dinâmica
- **Swagger UI**: http://localhost:5041

## Tecnologias

- **Framework**: ASP.NET Core 9.0 com Minimal APIs
- **Banco de Dados**: SQL Server 2022
- **ORM**: Entity Framework Core 9.0
- **Documentação**: Swagger/OpenAPI (Swashbuckle.AspNetCore)
- **Padrões**: Primary constructors, async/await, dependency injection

## Regras de Negócio

### Precificação Dinâmica (aplicada no PARKED)

- < 25% ocupação: 10% de desconto
- 25-50% ocupação: preço normal
- 50-75% ocupação: 10% acréscimo
- 75-100% ocupação: 25% acréscimo

### Cálculo de Tarifa (aplicado na SAÍDA)

1. Primeiros 30 minutos: GRÁTIS (grace period)
2. Após 30 min: Taxa horária (arredondada para cima) × PreçoBaseAplicado
3. Usa `entry_time` do evento, não horário do servidor

### Gerenciamento de Capacidade

- Rastreia ocupação por setor (não total do estacionamento)
- Atribui vagas com base em GPS do evento PARKED
- Marca vaga como ocupada até EXIT

## Configuração e Execução

### Pré-requisitos

- .NET 9.0 SDK
- SQL Server 2022 (ou Docker)
- Visual Studio 2022 ou VS Code
- Docker Desktop (para execução com Docker Compose)

## Executando o Projeto

### Opção 1: Docker Compose (Recomendado)

A forma mais simples de executar todo o sistema é usando Docker Compose:

```bash
# Iniciar todos os serviços (database, garage-simulator, parking-api)
docker-compose up -d --build

# Ver logs em tempo real
docker-compose logs -f

# Ver logs de um serviço específico
docker-compose logs -f parking-api

# Parar todos os serviços
docker-compose down

# Limpar dados e recomeçar do zero
docker-compose down -v
```

Após executar `docker-compose up`, as APIs estarão disponíveis em:

- **Garage Simulator**: `http://localhost:5000` (Swagger UI na raiz)
- **Parking Management**: `http://localhost:5041` (Swagger UI na raiz)
- **SQL Server**: `localhost:1433` (sa/YourStrong@Passw0rd)

### Opção 2: Execução Local

Se preferir executar localmente sem Docker:

#### Setup do Banco de Dados

```bash
cd src/Parking.Management.Api
dotnet ef database update
```

#### 1. Iniciar o Garage Simulator

```bash
cd src/Garage.Simulator.Api
dotnet run
```

#### 2. Iniciar o Parking Management

```bash
cd src/Parking.Management.Api
dotnet run
```

As APIs estarão disponíveis nos mesmos endereços listados acima.

## Testando a API

Você pode testar as APIs de três formas:

### 1. Swagger UI (Recomendado para testes interativos)

- Acesse `http://localhost:5000` para o Garage Simulator
- Acesse `http://localhost:5041` para o Parking Management
- Interface gráfica com documentação completa de todos os endpoints
- Permite testar diretamente do navegador

### Fluxo de Teste Completo

1. **Verificar configuração do estacionamento**:

```http
GET http://localhost:5000/garage
```

2. **Registrar entrada de veículo**:

```http
POST http://localhost:5041/webhook
Content-Type: application/json

{
  "license_plate": "ABC1234",
  "entry_time": "2025-01-28T10:00:00Z",
  "event_type": "ENTRY"
}
```

3. **Registrar estacionamento (atribuir vaga)**:

```http
POST http://localhost:5041/webhook
Content-Type: application/json

{
  "license_plate": "ABC1234",
  "lat": -23.561684,
  "lng": -46.655981,
  "event_type": "PARKED"
}
```

4. **Registrar saída**:

```http
POST http://localhost:5041/webhook
Content-Type: application/json

{
  "license_plate": "ABC1234",
  "exit_time": "2025-01-28T12:30:00Z",
  "event_type": "EXIT"
}
```

5. **Consultar receita**:

```http
GET http://localhost:5041/revenue?sector=A&date=2025-01-28
```




## Estrutura do Projeto

```
ParkManagement/
├── src/
│   ├── Garage.Simulator.Api/
│   │   ├── Models/
│   │   │   ├── GarageConfig.cs
│   │   │   ├── Sector.cs
│   │   │   └── GarageSpot.cs
│   │   ├── Dockerfile
│   │   └── Program.cs
│   │
│   └── Parking.Management.Api/
│       ├── Data/
│       │   └── ParkingDbContext.cs
│       ├── Models/
│       │   ├── VehicleEvent.cs
│       │   ├── ParkingSession.cs
│       │   ├── GarageSector.cs
│       │   └── GarageSpot.cs
│       ├── Services/
│       │   ├── IGarageService.cs
│       │   ├── GarageService.cs
│       │   ├── IParkingService.cs
│       │   └── ParkingService.cs
│       ├── Migrations/
│       ├── Dockerfile
│       └── Program.cs
│
├── tests/
│   └── Parking.Management.Api.Tests/
│       ├── Services/
│       │   └── ParkingServiceTests.cs
│       └── Helpers/
│           └── DbContextHelper.cs
│
├── .github/
│   └── copilot-instructions.md
├── docker-compose.yml
└── ParkManagement.sln
```

## SQL Queries Úteis
```sql
-- Consultar todas as vagas
SELECT * FROM [ParkManagementDb].[dbo].[GarageSpots]

-- Consultar todos os setores
SELECT * FROM [ParkManagementDb].[dbo].[GarageSectors]

-- Consultar todas as sessões de estacionamento
SELECT * FROM [ParkManagementDb].[dbo].[ParkingSessions]