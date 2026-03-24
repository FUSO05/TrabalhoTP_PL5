# Protocolo de Comunicação SENSOR/GATEWAY/SERVIDOR - One Health

## Visão Geral

Este protocolo define a comunicação entre três entidades principais:
- **SENSOR**: Recolhe dados ambientais
- **GATEWAY**: Agregador intermédio (valida e encaminha dados)
- **SERVIDOR**: Armazena e processa informação

## Estrutura de Mensagens

Todas as mensagens seguem o formato JSON com campos obrigatórios:
- `messageType`: tipo de mensagem
- `timestamp`: data/hora em ISO 8601

---

## 📋 DIAGRAMA DE SEQUÊNCIA

```
SENSOR → GATEWAY → SERVIDOR

SENSOR                 GATEWAY              SERVIDOR
  :                      :                    :
  |---CONNECT--------→   |
  |  {sensor_id}         |
  |                      |
  | ←--OK|ERROR----------|
  |
  |---REGISTER--------→  |
  | {sensor_id,          |
  |  token, zona}        |
  |                      |
  | ←--OK|ERROR----------|
  |
  |---DATA----------→    |
  | {sensor_id,          |VALIDAR
  |  timestamp,          |(sensor+tipo)
  |  tipo_dados []}      |
  |                      |---STORE-------→  |
  |                      | {dados           | armazena
  |                      |  validados}      | em ficheiro
  |                      |                  |
  |                      | ←--STORED|ERROR--|
  |
  |---HEARTBEAT------→   |
  | {sensor_id,          |(update last_sync)
  |  timestamp}          |
  |
  |---STREAM_REQUEST→    |
  | {video}              |
  |
  |---DATA----------→    |
  | {nova medição}       |--STORE-------→  |
  |                      |                  |
  |                      | ←--STORED|ERROR--|
  |
  |---DISCONNECT-----→   |
  | {sensor_id}          |
  |                      |
```

---

## MENSAGENS DETALHADAS

### 1. CONNECT (SENSOR → GATEWAY)
Sensor inicia comunicação
```json
{
  "messageType": "CONNECT",
  "sensorId": "S101",
  "timestamp": "2026-03-10T09:15:00Z"
}
```

**Resposta:**
```json
{
  "messageType": "RESPONSE",
  "originalMessageType": "CONNECT",
  "status": "OK",
  "message": null,
  "timestamp": "2026-03-10T09:15:01Z"
}
```

### 2. REGISTER (SENSOR → GATEWAY)
Sensor regista informações após confirmação de conexão
```json
{
  "messageType": "REGISTER",
  "sensorId": "S101",
  "token": "TOKEN_GERADO_GATEWAY",
  "zone": "ZONA_CENTRO",
  "supportedDataTypes": ["TEMP", "HUM", "RUIDO"],
  "timestamp": "2026-03-10T09:15:02Z"
}
```

**Resposta:**
```json
{
  "messageType": "RESPONSE",
  "originalMessageType": "REGISTER",
  "status": "OK",
  "message": "Sensor registado com sucesso",
  "timestamp": "2026-03-10T09:15:03Z"
}
```

Resposta de Erro:
```json
{
  "messageType": "RESPONSE",
  "originalMessageType": "REGISTER",
  "status": "ERROR",
  "message": "Sensor não registado na configuração ou em estado desativado",
  "timestamp": "2026-03-10T09:15:03Z"
}
```

### 3. DATA (SENSOR → GATEWAY)
Sensor envia medição ambiental
```json
{
  "messageType": "DATA",
  "sensorId": "S101",
  "dataType": "PM2.5",
  "value": 78,
  "timestamp": "2026-03-10T09:15:10Z"
}
```

O GATEWAY valida:
- Sensor está registado?
- Tipo de dado é suportado pelo sensor?

Se válido → STORE ao SERVIDOR

### 4. HEARTBEAT (SENSOR → GATEWAY)
Sensor envia sinal de vida
```json
{
  "messageType": "HEARTBEAT",
  "sensorId": "S101",
  "timestamp": "2026-03-10T09:20:00Z"
}
```

O GATEWAY atualiza `last_sync` do sensor no CSV

### 5. STREAM_REQUEST (SENSOR → GATEWAY)
Sensor requisita stream de vídeo
```json
{
  "messageType": "STREAM_REQUEST",
  "sensorId": "S101",
  "streamType": "VIDEO",
  "streamEndpoint": "rtsp://sensor:8080/stream",
  "timestamp": "2026-03-10T09:25:00Z"
}
```

### 6. DISCONNECT (SENSOR → GATEWAY)
Sensor termina comunicação
```json
{
  "messageType": "DISCONNECT",
  "sensorId": "S101",
  "timestamp": "2026-03-10T10:00:00Z"
}
```

---

## 🔄 MENSAGENS GATEWAY → SERVIDOR

### STORE (GATEWAY → SERVIDOR)
Gateway envia dados validados
```json
{
  "messageType": "STORE",
  "sensorId": "S101",
  "zone": "ZONA_CENTRO",
  "dataType": "PM2.5",
  "value": 78,
  "timestamp": "2026-03-10T09:15:10Z"
}
```

**Resposta:**
```json
{
  "messageType": "STORAGE_RESPONSE",
  "status": "STORED",
  "message": "Dados armazenados com sucesso",
  "timestamp": "2026-03-10T09:15:11Z"
}
```

Resposta de Erro:
```json
{
  "messageType": "STORAGE_RESPONSE",
  "status": "ERROR",
  "message": "Erro ao armazenar dados",
  "timestamp": "2026-03-10T09:15:11Z"
}
```

---

## Máquinas de Estado

### SENSOR
```
[Desconectado]
     ↓ enviar CONNECT
[Aguardando resposta]
     ↓ receber OK
[Enviando REGISTER]
     ↓ receber OK
[Operacional]
     ↓ periodicamente: HEARTBEAT
     ↓ quando necessário: DATA, STREAM_REQUEST
     ↓ enviar DISCONNECT
[Desconectado]
```

### GATEWAY
```
[Escutando]
     ↓ receber CONNECT
[Validando conexão]
     ↓ responder OK/ERROR
[Escutando]
     ↓ receber REGISTER
[Validando registro]
     ↓ responder OK/ERROR
[Monitorando]
     ↓ receber DATA/HEARTBEAT/STREAM_REQUEST
     ↓ validar (sensor + tipo_dado)
     ↓ encaminhar STORE ao SERVIDOR
[Monitorando]
     ↓ receber DISCONNECT
[Escutando]
```

### SERVIDOR
```
[Escutando]
     ↓ receber STORE do GATEWAY
[Validando dados]
     ↓ armazenar em ficheiro (por tipo_dado)
     ↓ responder STORED/ERROR
[Escutando]
```

---

## Ficheiros de Configuração

### sensors.csv (lido pelo GATEWAY)
```
sensor_id:estado:zona:tipos_dados:last_sync
S101:ativo:ZONA_CENTRO:TEMP,HUM,RUIDO:2026-03-10T08:45:00
S102:ativo:ZONA_ESCOLAR:PM2.5,TEMP:2026-03-10T09:00:00
S103:manutencao:ZONA_INDUSTRIAL:AR,PM10:2026-03-09T18:30:00
```

Estados válidos:
- **ativo**: Sensor em funcionamento
- **manutencao**: Sensor temporariamente indisponível
- **desativado**: Sensor removido

---

## Fases de Implementação

| Fase | Objetivo | Prazo |
|------|----------|-------|
| 1 | Desenho do protocolo | 16-20 mar |
| 2 | Implementação básica (sockets TCP) | 23-27 mar |
| 3 | Funcionalidade operacional completa | 7-10 abr |
| 4 | Atendimento concorrente (threads) | 13-17 abr |
