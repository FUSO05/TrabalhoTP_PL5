# Checklist de Implementação - Fase 2

## Objetivo
Implementação de um SERVIDOR, GATEWAY e SENSOR simples que comuniquem de acordo com o protocolo definido.

**Referência**: Ver `docs/PROTOCOL.md` para detalhes das mensagens.

---

## SENSOR

### Conexão Inicial
- [ ] Conectar ao GATEWAY via socket TCP (endereço IP + porta fornecidos como parâmetros)
- [ ] Enviar mensagem **CONNECT** com:
  - `sensorId`
  - `timestamp`
- [ ] Receber **ResponseMessage** (OK | ERROR)
- [ ] Se OK, proceder para REGISTER
- [ ] Se ERROR, mostrar erro e desconectar

### Registo
- [ ] Receber token do GATEWAY (contido no OK da resposta)
- [ ] Enviar mensagem **REGISTER** com:
  - `sensorId`
  - `token` (recebido no CONNECT)
  - `zone` (ZONA_CENTRO, ZONA_ESCOLAR, etc)
  - `supportedDataTypes` (lista de tipos suportados)
  - `timestamp`
- [ ] Receber **ResponseMessage** (OK | ERROR)
- [ ] Se OK, sensor operacional
- [ ] Se ERROR, mostrar erro e desconectar

### Operação
- [ ] Interface de texto simples com menu:
  - [ ] Opção 1: Enviar medição (DATA)
  - [ ] Opção 2: Enviar heartbeat (HEARTBEAT)
  - [ ] Opção 3: Requisitar stream (STREAM_REQUEST)
  - [ ] Opção 4: Desconectar (DISCONNECT)
  - [ ] Opção 0: Sair

### Envio de Dados
- [ ] Enviar mensagem **DATA** com:
  - `sensorId`
  - `dataType` (TEMP, HUM, PM2.5, RUIDO, AR, LUMINOSIDADE, PM10, VIDEO)
  - `value` (double)
  - `timestamp`

### Heartbeat
- [ ] Enviar mensagem **HEARTBEAT** periodicamente (ex: a cada 30 segundos)
  - `sensorId`
  - `timestamp`

### Stream de Vídeo (Opcional)
- [ ] Enviar mensagem **STREAM_REQUEST** com:
  - `sensorId`
  - `streamType` (VIDEO)
  - `streamEndpoint` (URL/socket do stream)
  - `timestamp`

### Desconexão
- [ ] Enviar mensagem **DISCONNECT** com:
  - `sensorId`
  - `timestamp`
- [ ] Fechar socket
- [ ] Graceful shutdown

---

## GATEWAY

### Inicialização
- [ ] Ler ficheiro de configuração `config/sensors.csv`
  - [ ] Parsear formato: `sensor_id:estado:zona:tipos_dados:last_sync`
  - [ ] Armazenar em estrutura de dados em memória
- [ ] Escutar conexões TCP na porta fornecida (ex: 9000)
- [ ] Criar estrutura para rastrear sensores conectados

### Recepção de CONNECT
- [ ] Receber mensagem **CONNECT** do SENSOR
- [ ] Extrair `sensorId`
- [ ] Verificar se sensor existe em `sensors.csv`
  - [ ] Se SIM: gerar token e enviar **ResponseMessage** OK com token
  - [ ] Se NÃO: enviar **ResponseMessage** ERROR

### Recepção de REGISTER
- [ ] Receber mensagem **REGISTER** do SENSOR
- [ ] Validar sensor:
  - [ ] Verificar se `sensorId` está registado
  - [ ] Verificar se token é válido
  - [ ] Verificar se estado != DESATIVADO
  - [ ] Verificar se `zone` coincide com CSV
  - [ ] Verificar se `supportedDataTypes` estão em CSV
- [ ] Se válido:
  - [ ] Enviar **ResponseMessage** OK
  - [ ] Atualizar `last_sync` do sensor
  - [ ] Adicionar sensor à lista de sensores ativos
- [ ] Se inválido:
  - [ ] Enviar **ResponseMessage** ERROR com motivo

### Recepção de DATA
- [ ] Receber mensagem **DATA** do SENSOR
- [ ] Validar dados:
  - [ ] Verificar se sensor está registado e ativo
  - [ ] Verificar se `dataType` está em `supportedDataTypes` do sensor
  - [ ] Verificar se `value` está dentro de limites razoáveis
- [ ] Se válido:
  - [ ] Encaminhar para SERVIDOR via mensagem **STORE**
  - [ ] Esperar **StorageResponseMessage** (STORED | ERROR)
  - [ ] Se STORED: atualizar `last_sync` do sensor
- [ ] Se inválido:
  - [ ] Descartar dados
  - [ ] Opcionalmente: registar erro em log

### Recepção de HEARTBEAT
- [ ] Receber mensagem **HEARTBEAT** do SENSOR
- [ ] Atualizar `last_sync` do sensor no CSV
- [ ] Opcionalmente: responder com **ResponseMessage** OK

### Recepção de STREAM_REQUEST
- [ ] Receber mensagem **STREAM_REQUEST** do SENSOR
- [ ] Validar pedido
- [ ] Encaminhar para SERVIDOR (se aplicável)
- [ ] Responder com **ResponseMessage** OK | ERROR

### Recepção de DISCONNECT
- [ ] Receber mensagem **DISCONNECT** do SENSOR
- [ ] Remover sensor da lista de sensores ativos
- [ ] Fechar socket
- [ ] Responder com **ResponseMessage** OK

### Monitorização de Sensores
- [ ] Thread separada para verificar heartbeat timeout
  - [ ] Se `current_time - last_sync > X segundos`: marcar sensor como desconectado
  - [ ] Atualizar status em CSV para "desativado" temporariamente

### Ficheiro CSV
- [ ] Ler `config/sensors.csv` ao iniciar
- [ ] Atualizar `last_sync` após cada operação (thread-safe com lock)
- [ ] Implementar lock/mutex para acesso ao CSV
- [ ] Escrever alterações imediatamente

---

## SERVIDOR

### Inicialização
- [ ] Escutar conexões TCP na porta fornecida (ex: 8000)
- [ ] Criar diretório `data/` se não existir
- [ ] Inicializar estrutura para armazenar dados

### Recepção de STORE (GATEWAY)
- [ ] Receber mensagem **STORE** do GATEWAY
- [ ] Extrair dados:
  - `sensorId`
  - `zone`
  - `dataType`
  - `value`
  - `timestamp`

### Validação de Dados
- [ ] Verificar se `sensorId` não está vazio
- [ ] Verificar se `dataType` é válido (TEMP, HUM, PM2.5, RUIDO, AR, LUMINOSIDADE, PM10)
- [ ] Verificar se `value` está dentro de limites razoáveis (ex: TEMP entre -50 e 60°C)
- [ ] Verificar se `timestamp` está presente

### Armazenamento
- [ ] Armazenar dados em ficheiro `data/measurements_DATATYPE.txt`
  - [ ] Criar ficheiro se não existir
  - [ ] Formato de cada linha: `timestamp:sensor_id:zona:tipo_dado:valor`
  - [ ] Exemplo: `2026-03-10T09:15:00:S101:ZONA_CENTRO:PM2.5:78`
- [ ] Usar lock/mutex para acesso thread-safe ao ficheiro
- [ ] Fazer flush imediatamente (garantir persistência)

### Resposta
- [ ] Se armazenamento bem-sucedido:
  - [ ] Enviar **StorageResponseMessage** com status STORED
- [ ] Se erro:
  - [ ] Enviar **StorageResponseMessage** com status ERROR e mensagem de erro

### Funcionalidades Opcionais (Fase 3+)
- [ ] Implementar base de dados relacional (SQLite)
- [ ] Consultas/análise de dados
- [ ] API REST

---

## 🧪 Testes

### Teste 1: Inicialização Básica
- [ ] Iniciar SERVIDOR na porta 8000
- [ ] Iniciar GATEWAY na porta 9000 com `config/sensors.csv`
- [ ] Verificar se ambos estão à escuta

### Teste 2: Sensor Válido
- [ ] Iniciar SENSOR com `S101 127.0.0.1 9000`
- [ ] Enviar CONNECT
- [ ] Receber ResponseMessage OK
- [ ] Enviar REGISTER com token
- [ ] Receber ResponseMessage OK
- [ ] Enviar DATA (TEMP: 22.5)
- [ ] Verificar se dados foram armazenados em `data/measurements_TEMP.txt`
- [ ] Verificar se `last_sync` foi atualizado em CSV

### Teste 3: Sensor Inválido
- [ ] Iniciar SENSOR com `S999 127.0.0.1 9000` (sensor não registado)
- [ ] Enviar CONNECT
- [ ] Receber ResponseMessage ERROR
- [ ] Desconectar

### Teste 4: Tipo de Dado Inválido
- [ ] Sensor S101 tenta enviar RUIDO (não suportado)
- [ ] GATEWAY rejeita e não encaminha ao SERVIDOR
- [ ] Ficheiro de dados não é criado

### Teste 5: Múltiplos Sensores
- [ ] Iniciar 3 sensores diferentes (S101, S102, S103)
- [ ] Todos enviam dados simultaneamente
- [ ] Verificar se todos os dados foram armazenados corretamente
- [ ] Verificar thread-safety

### Teste 6: Heartbeat e Timeout
- [ ] Sensor envia HEARTBEAT a cada 10 segundos
- [ ] GATEWAY atualiza `last_sync`
- [ ] Se sensor não enviar heartbeat por 30 segundos: marcar como desconectado

### Teste 7: Desconexão Graciosa
- [ ] Sensor envia DISCONNECT
- [ ] GATEWAY remove da lista ativa
- [ ] Socket é fechado corretamente

---

## 📅 Prazos
**Semana de 23 a 27 de março**

| Componente | Prazo |
|------------|-------|
| SERVIDOR básico | até 25 mar |
| GATEWAY básico | até 26 mar |
| SENSOR com interface | até 27 mar |
| Testes integrados | até 27 mar |

---

## ✅ Checklist de Entrega

- [ ] Código compilável sem erros
- [ ] Todas as mensagens implementadas conforme protocolo
- [ ] Testes passando (pelo menos Testes 1-7)
- [ ] Documentação atualizada
- [ ] Código no repositório Git
- [ ] Apresentação pronta para aula PL

