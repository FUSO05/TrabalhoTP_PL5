# Fase 1 - Desenho do Protocolo

## COMPLETO

### Trabalho Realizado

#### Protocolo de Comunicação Definido
- [x] Estrutura base de mensagens (JSON)
- [x] Mensagens específicas: SENSOR_INIT, DATA, HEARTBEAT, DISCONNECT, ACK
- [x] Máquinas de estado para SENSOR, GATEWAY e SERVIDOR
- [x] Validação de sensores contra ficheiro CSV
- [x] Atualização de last_sync

#### Tipos de Dados Suportados
- [x] TEMP (Temperatura)
- [x] HUM (Humidade)
- [x] PM2.5 (Partículas)
- [x] PM10 (Partículas)
- [x] RUIDO (Nível de Ruído)
- [x] AR (Qualidade do Ar)
- [x] LUMINOSIDADE
- [x] VIDEO (Streams de vídeo)

#### Zonas da Cidade
- [x] ZONA_CENTRO
- [x] ZONA_ESCOLAR
- [x] ZONA_INDUSTRIAL
- [x] ZONA_RESIDENCIAL
- [x] ZONA_PARQUE

#### Estados do Sensor
- [x] ATIVO
- [x] MANUTENCAO
- [x] DESATIVADO

### Ficheiros Criados
- `docs/PROTOCOL.md` - Especificação completa do protocolo
- `src/Common/Protocol/Messages.cs` - Definições de mensagens
- `src/Common/Models/Entities.cs` - Modelos e enums
- `config/sensors.csv` - Exemplo de configuração

### Próximas Passos (Fase 2)

1. Implementar comunicação por sockets (TCP)
2. Implementar SENSOR com interface de texto
3. Implementar GATEWAY com validação de sensores
4. Implementar SERVIDOR básico com armazenamento de dados
5. Testar comunicação entre componentes

---

**Data**: Março 2026
