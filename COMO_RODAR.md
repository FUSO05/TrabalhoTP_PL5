# Como rodar o One Health

Abre **6 terminais** separados. Todos a partir da pasta raiz do projeto:
```
cd "c:\Users\lnlui\OneDrive\Desktop\Projects\SD_TP\One Health\One Health"
```

---

## Ordem de arranque (respeitar a ordem!)

### Terminal 1 — RabbitMQ (já deve estar a correr como serviço)
Verifica se está ativo:
```
http://localhost:15672
```
Login: `guest` / `guest`
Se não estiver a correr, inicia o serviço Windows:
```powershell
Start-Service -Name RabbitMQ
```

---

### Terminal 2 — Servidor (dashboard + TCP listener)
```powershell
dotnet run --project src/Server
```
- Dashboard web: http://localhost:8080
- TCP listener para os Gateways: porta 8000

---

### Terminal 3 — PreProcessor (gRPC)
```powershell
dotnet run --project src/PreProcessor
```
- Porta gRPC: 50051

---

### Terminal 4 — Analysis Service (Python gRPC)
```powershell
cd src/AnalysisService
pip install -r requirements.txt   # só na primeira vez
python service.py
```
- Porta gRPC: 50052

---

### Terminal 5 — Gateway GW1
```powershell
dotnet run --project src/Gateway -- config/sensors.csv 127.0.0.1 8000 localhost http://localhost:50051 GW1
```

### Terminal 6 — Gateway GW2
```powershell
dotnet run --project src/Gateway -- config/sensors.csv 127.0.0.1 8000 localhost http://localhost:50051 GW2
```

---

### Terminais 7, 8, 9 — Sensores (um por sensor ativo)
```powershell
# Sensor S101 (ativo — ZONA_CENTRO: TEMP, HUM, RUIDO)
dotnet run --project src/Sensor -- S101

# Sensor S102 (ativo — ZONA_ESCOLAR: PM2.5, TEMP)
dotnet run --project src/Sensor -- S102

# Sensor S104 (ativo — ZONA_RESIDENCIAL: HUM, LUMINOSIDADE)
dotnet run --project src/Sensor -- S104
```
> S103 (manutenção) e S105 (desativado) mostram mensagem e terminam automaticamente.

---

## Intervalo de envio de dados
Por defeito: **60 segundos** por tipo de dado.
Para demo mais rápida (ex: 10 segundos):
```powershell
dotnet run --project src/Sensor -- S101 localhost config/sensors.csv 10000
```

---

## Arquitetura resumida
```
Sensor → RabbitMQ → Gateway → gRPC PreProcessor (50051)
                                    ↓
                              Servidor TCP (8000) → SQLite → Dashboard (8080)
                                    ↓
                         gRPC Analysis Service (50052)
```

---

## Limpar filas RabbitMQ (se houver backlog)
```powershell
$cred = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("guest:guest"))
$h = @{ Authorization = "Basic $cred" }
Invoke-RestMethod -Uri "http://localhost:15672/api/queues/%2F/gateway_data_GW1/contents"      -Method DELETE -Headers $h
Invoke-RestMethod -Uri "http://localhost:15672/api/queues/%2F/gateway_data_GW2/contents"      -Method DELETE -Headers $h
Invoke-RestMethod -Uri "http://localhost:15672/api/queues/%2F/gateway_heartbeat_GW1/contents" -Method DELETE -Headers $h
Invoke-RestMethod -Uri "http://localhost:15672/api/queues/%2F/gateway_heartbeat_GW2/contents" -Method DELETE -Headers $h
```
