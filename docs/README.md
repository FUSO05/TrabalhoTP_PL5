# Estrutura do Projeto One Health

## Organização de Pastas

```
One Health/
├── src/                          # Código fonte
│   ├── Common/                   # Código comum reutilizável
│   │   ├── Protocol/            # Definições do protocolo de comunicação
│   │   │   └── Messages.cs
│   │   └── Models/              # Modelos de dados comuns
│   │       └── Entities.cs
│   ├── Sensor/                  # Implementação do SENSOR
│   │   └── Program.cs
│   ├── Gateway/                 # Implementação do GATEWAY
│   │   └── Program.cs
│   └── Server/                  # Implementação do SERVIDOR
│       └── Program.cs
├── config/                       # Ficheiros de configuração
│   └── sensors.csv             # Configuração dos sensores registados
├── docs/                         # Documentação
│   ├── PROTOCOL.md             # Especificação do protocolo
│   └── README.md               # Este ficheiro
├── data/                         # Diretório para dados armazenados (criado em runtime)
└── One Health.csproj           # Ficheiro de projeto .NET
```

## Componentes

### 1. **Common** - Código Reutilizável
- `Protocol/Messages.cs`: Definições das mensagens do protocolo (SENSOR_INIT, DATA, HEARTBEAT, etc.)
- `Models/Entities.cs`: Modelos de dados (Sensor, EnvironmentalMeasurement, enums, etc.)

### 2. **Sensor** - Cliente Sensor
- Estabelece comunicação com o GATEWAY
- Identifica-se com sensor_id
- Envia dados ambientais
- Envia heartbeat periodicamente
- Interface de texto para simulação

### 3. **Gateway** - Agregador Intermédio
- Escuta conexões de SENSORes
- Valida sensores contra ficheiro CSV (sensors.csv)
- Verifica estado e tipos de dados suportados
- Atualiza last_sync
- Encaminha dados para o SERVIDOR
- Monitoriza heartbeat

### 4. **Server** - Armazenamento e Processamento
- Escuta conexões do GATEWAY
- Recebe dados ambientais
- Armazena dados em ficheiros (organizados por tipo)
- Fornece interface para consultas

## Ficheiros de Configuração

### sensors.csv
Formato: `sensor_id:estado:zona:tipos_dados:last_sync`

Exemplo:
```
S101:ativo:ZONA_CENTRO:TEMP,HUM,RUIDO:2026-03-10T08:45:00
S102:ativo:ZONA_ESCOLAR:PM2.5,TEMP:2026-03-10T09:00:00
```

## Fases de Implementação

| Fase | Descrição | Prazo |
|------|-----------|-------|
| 1 | Desenho do protocolo | 16-20 mar |
| 2 | Implementação básica | 23-27 mar |
| 3 | Funcionalidade de operação SENSOR | 7-10 abr |
| 4 | Atendimento concorrente | 13-17 abr |

## Próximos Passos

Iniciar Fase 2 com implementação dos componentes básicos usando sockets em C#.
