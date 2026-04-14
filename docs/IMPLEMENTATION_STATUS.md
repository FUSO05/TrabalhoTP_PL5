# One Health - Fase 2 Implementação Completa

## Status: ✅ COMPLETO

A implementação da **Fase 2** ("Implementação de um SERVIDOR, GATEWAY e de SENSOR simples") está **100% completa** com todas as funcionalidades operacionais.

---

## Componentes Implementados

### 1. 📦 SERVER (src/Server/Program.cs)
**Responsabilidades:**
- TCP listener na porta 8000
- Recepção de mensagens STORE do GATEWAY
- Validação de dados (ranges, tipos, campos obrigatórios)
- Armazenamento em ficheiros por tipo de dado
- Respostas STORAGE_RESPONSE (STORED/ERROR)
- **Thread-safe:** Locks para acesso a ficheiros

**Dados Validados:**
- TEMP: -50°C a 60°C
- HUM: 0% a 100%
- PM2.5, PM10: 0 a 1000 µg/m³
- RUIDO: 30 a 130 dB
- AR: 0 a 500 AQI
- LUMINOSIDADE: 0 a 150000 lux
- VIDEO: Apenas armazena metadados

**Ficheiros Gerados:**
```
data/measurements_TEMP.txt
data/measurements_HUM.txt
data/measurements_PM2.5.txt
... (um por cada tipo de dado)
```

---

### 2. 🌐 GATEWAY (src/Gateway/Program.cs)
**Responsabilidades:**
- TCP listener na porta 9000
- Gestão de sensores configurados (CSV)
- Validação de CONNECT, REGISTER, DATA, HEARTBEAT, DISCONNECT
- Roteamento de dados para SERVER
- Monitorização de heartbeat (timeout: 30s)
- **Thread-safe:** Locks para dicionário de sensores, CSV

**Protocolo de Validação:**
1. **CONNECT:** Verifica se sensor existe em sensors.csv
2. **REGISTER:** Valida token, status, zona, tipos de dados
3. **DATA:** Valida se sensor está registado, se tipo é suportado
4. **HEARTBEAT:** Atualiza last_sync timestamp
5. **STREAM_REQUEST:** Aceita requisição de streams

**Ficheiro de Configuração:**
```csv
sensor_id:estado:zona:tipos_dados:last_sync
S101:ativo:ZONA_CENTRO:TEMP,HUM,RUIDO:2026-04-14T09:25:45Z
S102:ativo:ZONA_ESCOLAR:PM2.5,TEMP:2026-04-14T09:25:45Z
S103:manutencao:ZONA_INDUSTRIAL:AR,PM10:2026-04-14T09:25:45Z
S104:ativo:ZONA_RESIDENCIAL:HUM,LUMINOSIDADE:2026-04-14T09:25:45Z
S105:desativado:ZONA_PARQUE:TEMP,RUIDO:2026-04-14T09:25:45Z
```

---

### 3. 📡 SENSOR (src/Sensor/Program.cs)
**Responsabilidades:**
- TCP client que se conecta ao GATEWAY
- Interface de menu interativo (5 opções)
- Envio de medições (DATA) com validação de ranges
- Heartbeat automático (30 segundos, background thread)
- Geração aleatória de dados para testes
- **Thread-safe:** CancellationToken para heartbeat

**Menu Interativo:**
```
1. Enviar medição (DATA)      → Seleciona tipo, valor manual ou aleatório
2. Enviar heartbeat           → Envio manual de heartbeat
3. Requisitar stream          → Submete endpoint para stream
4. Desconectar               → DISCONNECT e encerramento
0. Sair                       → Encerra aplicação
```

**Fluxo de Conexão:**
1. Conecta ao GATEWAY na porta 9000
2. Envia CONNECT {SensorId}
3. Recebe token
4. Envia REGISTER com token, zona, tipos suportados
5. Abre menu para interação
6. Heartbeat automático a cada 30 segundos
7. Encerramento gracioso com DISCONNECT

---

## Protocolo de Comunicação

### Mensagens Implementadas

```
SENSOR → GATEWAY:
├── CONNECT {sensor_id}
├── REGISTER {sensor_id, token, zone, supportedDataTypes}
├── DATA {sensor_id, dataType, value}
├── HEARTBEAT {sensor_id}
├── STREAM_REQUEST {sensor_id, streamType, streamEndpoint}
└── DISCONNECT {sensor_id}

GATEWAY → SERVER:
└── STORE {sensor_id, zone, dataType, value}

Respostas:
├── RESPONSE {originalMessageType, status: OK|ERROR, message}
└── STORAGE_RESPONSE {status: STORED|ERROR, message}
```

### Factory Polimórfica (MessageFactory)
Implementação de desserialização polymórfica para resolver issues com tipos abstratos:
- Extrai `messageType` do JSON
- Desserializa para tipo concreto apropriado
- Evita erro: "Deserialization of interface or abstract types is not supported"

---

## Estrutura de Ficheiros

```
One Health/
├── OneHealth.sln                           # Solution file
├── Program.cs                              # Entry point com instruções
├── config/
│   └── sensors.csv                         # Configuração de sensores
├── data/
│   ├── measurements_TEMP.txt               # Dados armazenados
│   ├── measurements_HUM.txt
│   └── ... (um por cada tipo de dado)
├── docs/
│   ├── PROTOCOL.md                         # Especificação completa do protocolo
│   └── TESTING_GUIDE.md                    # Guia de testes
├── src/
│   ├── Common/
│   │   ├── Models/Entities.cs              # Enums de tipos de dados
│   │   ├── Protocol/Messages.cs            # Definições de mensagens + MessageFactory
│   │   └── Utilities/MessageSerializer.cs  # JSON serialization
│   ├── Server/
│   │   ├── Server.csproj
│   │   └── Program.cs                      # Implementação completa
│   ├── Gateway/
│   │   ├── Gateway.csproj
│   │   ├── Program.cs                      # Implementação completa
│   │   ├── SensorConfiguration.cs          # Model + parsing CSV
│   │   └── SensorConfigurationManager.cs   # Thread-safe CSV manager
│   └── Sensor/
│       ├── Sensor.csproj
│       ├── Program.cs                      # Implementação completa
│       └── SensorDefaults.cs               # Test data + ranges
└── One Health.csproj                       # Main project file
```

---

## Testes Executados

✅ **Conectividade SENSOR → GATEWAY → SERVER**
- Fluxo completo: CONNECT → REGISTER → DATA → STORE

✅ **Validação de Dados**
- Ranges de valores (TEMP, HUM, PM2.5, etc.)
- Tipos de dados suportados por sensor
- Campos obrigatórios

✅ **Thread-Safety**
- Múltiplas conexões simultâneas
- Acesso concorrente a ficheiros
- Acesso concorrente a sensores.csv

✅ **Heartbeat**
- Envio periódico (30s)
- Timeout detection (30s sem heartbeat)

✅ **Estados de Sensor**
- `ativo` → Funciona normalmente
- `manutencao` → Rejeita REGISTER
- `desativado` → Rejeita CONNECT

✅ **Error Handling**
- Sensor não registado
- Tipo de dado não suportado
- Valores fora de range

---

## Commits Git

```
c67273b - Docs: Adicionar guia completo de testes para Fase 2
08286cf - Fix: Implementar factory polimórfica para desserialização de mensagens
bd81fc0 - Feat: Implementação completa do SENSOR (Ponto 3)
aaf192e - Feat: Implementação completa do GATEWAY (Ponto 2)
13209b7 - Feat: Implementação completa do SERVIDOR (Ponto 1)
```

---

## Como Executar

### Build
```bash
cd "One Health"
dotnet build
```

### Teste (3 terminais)

**Terminal 1 - SERVER:**
```bash
dotnet run --project src/Server -- 8000
```

**Terminal 2 - GATEWAY:**
```bash
dotnet run --project src/Gateway -- 9000
```

**Terminal 3 - SENSOR (S101):**
```bash
dotnet run --project src/Sensor -- S101 127.0.0.1 9000
```

---

## Próximas Fases

### Fase 3 (7-10 Abril)
- Automatização de dados no SENSOR
- Processamento em GATEWAY
- Base de dados relacional (opcional)

### Fase 4 (13-17 Abril)
- Otimização de concorrência
- Reader-writer locks
- Performance testing

---

## Notas de Implementação

1. **Protocolo JSON:** Todas as mensagens em JSON com camelCase
2. **Thread-Safety:** Locks para acesso a recursos compartilhados
3. **Validação:** Ranges de valores conforme especificação
4. **Error Handling:** Respostas ERROR em todas as falhas
5. **CSV Management:** Atualização automática de last_sync
6. **Heartbeat:** Thread background com CancellationToken
7. **Factory Pattern:** Desserialização polymórfica de mensagens

---

## Conformidade com Requisitos

✅ Protocolo definido em PROTOCOL.md
✅ SENSOR com interface de texto simples
✅ GATEWAY com validação e roteamento
✅ SERVER com armazenamento em ficheiro
✅ Comunicação via sockets TCP
✅ JSON para serialização
✅ C# .NET 10
✅ Múltiplas conexões simultâneas
✅ Heartbeat e timeout detection
✅ Estados de sensor (ativo/manutencao/desativado)

---

**Data de Conclusão:** 14 Abril 2026
**Repositório:** https://github.com/FUSO05/TrabalhoTP_PL5
**Branch:** main
