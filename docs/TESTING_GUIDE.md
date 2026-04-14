# Guia de Testes - One Health Sistema de Monitorização

## Overview
Este documento descreve como testar a implementação da Fase 2 com os três componentes: SERVER, GATEWAY, e SENSOR.

---

## Pré-requisitos

1. **Build da solução:**
   ```bash
   cd "One Health"
   dotnet build
   ```

2. **Configuração dos sensores** (config/sensors.csv):
   - S101: Suporta TEMP, HUM, RUIDO
   - S102: Suporta PM2.5, TEMP
   - S103: Em manutenção (AR, PM10)
   - S104: Suporta HUM, LUMINOSIDADE
   - S105: Desativado (TEMP, RUIDO)

---

## Teste Completo (3 Terminais)

### Terminal 1 - Iniciar SERVER

```bash
cd "One Health"
dotnet run --project src/Server -- 8000
```

**Saída esperada:**
```
╔════════════════════════════════════════════════════════════╗
║   ONE HEALTH - SERVIDOR                                  ║
║   Aguardando conexões na porta 8000...                   ║
╚════════════════════════════════════════════════════════════╝
```

---

### Terminal 2 - Iniciar GATEWAY

```bash
cd "One Health"
dotnet run --project src/Gateway -- 9000
```

**Saída esperada:**
```
╔════════════════════════════════════════════════════════════╗
║   ONE HEALTH - GATEWAY                                    ║
║   Conectando ao servidor 127.0.0.1:8000...              ║
✓ Conectado ao servidor 127.0.0.1:8000
  ✓ Sensor carregado: S101 (ativo) - ZONA_CENTRO
  ✓ Sensor carregado: S102 (ativo) - ZONA_ESCOLAR
  ✓ Sensor carregado: S103 (manutencao) - ZONA_INDUSTRIAL
  ✓ Sensor carregado: S104 (ativo) - ZONA_RESIDENCIAL
  ✓ Sensor carregado: S105 (desativado) - ZONA_PARQUE
Aguardando conexões na porta 9000...
```

---

### Terminal 3 - Iniciar SENSOR (S101)

```bash
cd "One Health"
dotnet run --project src/Sensor -- S101 127.0.0.1 9000
```

**Saída esperada:**
```
=== ONE HEALTH SENSOR ===
Fase 2 - Implementação de SENSOR simples

Sensor ID: S101
Gateway: 127.0.0.1:9000

Conectando ao Gateway em 127.0.0.1:9000...
Conectado ao Gateway!
Token recebido: UzEwMToyMDI2LTA0LTE0VDA5OjI0OjU4Ljg3NDk2NzNa
Registado com sucesso!

--- MENU ---
1. Enviar medição (DATA)
2. Enviar heartbeat (HEARTBEAT)
3. Requisitar stream (STREAM_REQUEST)
4. Desconectar (DISCONNECT)
0. Sair
Opção: 
```

---

## Fluxo de Teste - Enviar Dados

### 1. Enviar medição de TEMPERATURA (TEMP)

**Input:** Opção 1 (Enviar medição)
```
Tipos de dados disponíveis:
1. TEMP
2. HUM
3. PM2.5
4. RUIDO
5. AR
6. LUMINOSIDADE
7. PM10
8. VIDEO
Selecione tipo de dado (número): 1
Valor (enter para gerar aleatório): [Enter]
```

**Saída esperada SENSOR:**
```
Valor gerado aleatoriamente: 23.45
Medição enviada com sucesso!
```

**Saída esperada GATEWAY:**
```
→ Conexão recebida de 127.0.0.1:XXXXX
📨 Mensagem recebida: {"sensorId":"S101"...
🔗 CONNECT: S101
✓ Token gerado: UzEwMToyMDI2...
📨 Mensagem recebida: {"sensorId":"S101"...
📝 REGISTER: S101
✓ Sensor S101 registado com sucesso
📨 Mensagem recebida: {"sensorId":"S101"...
📊 DATA: S101 - TEMP=23.45
✓ Dados encaminhados para servidor
```

**Saída esperada SERVER:**
```
→ Conexão recebida de 127.0.0.1:XXXXX
📨 Mensagem recebida: {"sensorId":"S101"...
📦 STORE: Sensor=S101, Tipo=TEMP, Valor=23.45
✓ Dados armazenados em measurements_TEMP.txt
```

---

### 2. Testar validação de TIPO DE DADO NÃO SUPORTADO

**Teste:** Tentar enviar PM2.5 do S101 (que não suporta)

**Saída esperada GATEWAY:**
```
❌ Tipo de dado PM2.5 não suportado por S101
```

**Solução:** Usar S102 que suporta PM2.5

```bash
dotnet run --project src/Sensor -- S102 127.0.0.1 9000
```

---

### 3. Enviar HEARTBEAT

**Input:** Opção 2 (Enviar heartbeat)

**Saída esperada SENSOR:**
```
Heartbeat enviado.
```

**Saída esperada GATEWAY:**
```
📨 Mensagem recebida: {"sensorId":"S101"...
💓 HEARTBEAT: S101 (last_sync atualizado)
```

---

### 4. Requisitar STREAM

**Input:** Opção 3

```
Endpoint do stream (ex: 192.168.1.100:5000): 192.168.1.100:5000
```

**Saída esperada:**
```
Resposta: Stream requisitado para VIDEO no endpoint 192.168.1.100:5000
```

---

### 5. DESCONECTAR

**Input:** Opção 4 ou 0 (Desconectar)

**Saída esperada SENSOR:**
```
Mensagem de desconexão enviada.
Sensor desconectado.
```

**Saída esperada GATEWAY:**
```
📨 Mensagem recebida: {"sensorId":"S101"...
👋 DISCONNECT: S101
✓ Sensor S101 desconectado
← Conexão fechada
× Sensor S101 desconectado
```

---

## Testes de Robustez

### Teste 1: Sensor em Manutenção

```bash
dotnet run --project src/Sensor -- S103 127.0.0.1 9000
```

**Saída esperada:**
```
Gateway rejeitou conexão: Sensor em estado: manutencao
```

---

### Teste 2: Sensor Desativado

```bash
dotnet run --project src/Sensor -- S105 127.0.0.1 9000
```

**Saída esperada:**
```
Gateway rejeitou conexão: Sensor em estado: desativado
```

---

### Teste 3: Sensor Não Registado

```bash
dotnet run --project src/Sensor -- S999 127.0.0.1 9000
```

**Saída esperada:**
```
Gateway rejeitou conexão: Sensor S999 não está registado
```

---

### Teste 4: Múltiplas Conexões Simultâneas

**Terminal 4 (novo):**
```bash
dotnet run --project src/Sensor -- S104 127.0.0.1 9000
```

**Saída esperada:** Ambos os sensores funcionam simultaneamente sem interferência

---

## Ficheiros de Dados Gerados

Após execução bem-sucedida, o SERVER cria ficheiros:

```
data/
├── measurements_TEMP.txt
├── measurements_HUM.txt
├── measurements_PM2.5.txt
├── measurements_RUIDO.txt
├── measurements_AR.txt
├── measurements_LUMINOSIDADE.txt
├── measurements_PM10.txt
└── measurements_VIDEO.txt
```

**Formato de cada ficheiro:**
```
2026-04-14T09:25:45.1838573Z:S101:ZONA_CENTRO:TEMP:23.45
2026-04-14T09:26:10.5934821Z:S101:ZONA_CENTRO:TEMP:25.67
```

---

## Troubleshooting

### Erro: "Deserialization of interface or abstract types is not supported"
**Causa:** Versão antiga do código sem o MessageFactory
**Solução:** Executar `git pull` e rebuildar

### Erro: "Tipo de dado XYZ não suportado"
**Causa:** Sensor não suporta este tipo de dado
**Solução:** Verificar config/sensors.csv para os tipos suportados por cada sensor

### Erro: "Sensor não está registado"
**Causa:** Sensor ainda não enviou mensagem REGISTER
**Solução:** Servidor recebe apenas após REGISTER bem-sucedido

### Conexão fecha após primeira mensagem
**Causa:** Deserialization error (resolvido no commit 08286cf)
**Solução:** Atualizar para versão mais recente com MessageFactory

---

## Logs Importantes

### SERVER
- ✓ STORE bem-sucedido
- ❌ Validação de valor fora de range
- 📊 Dados armazenados em ficheiro

### GATEWAY
- 🔗 CONNECT validado
- 📝 REGISTER bem-sucedido
- 📊 DATA validado e roteado
- 💓 HEARTBEAT recebido
- ❌ Tipo de dado não suportado
- × Sensor desconectado

### SENSOR
- ✓ Conectado ao Gateway
- 🔑 Token recebido
- 📤 Dados enviados
- 💓 Heartbeat automático (30s)

---

## Próximos Passos (Fase 3 e 4)

- [ ] Implementar automatização de dados no SENSOR
- [ ] Adicionar suporte a streams de vídeo
- [ ] Implementar base de dados relacional (opcional)
- [ ] Otimizar concorrência com reader-writer locks
- [ ] Adicionar persistência de configuração
