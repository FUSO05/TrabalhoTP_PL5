"""
One Health – Analysis Service (Python gRPC)
Performs statistical analysis, pollution pattern detection, and health risk assessment.
"""

import grpc
import math
import statistics
from concurrent import futures
from datetime import datetime, timezone

import analysis_pb2
import analysis_pb2_grpc

# WHO / reference thresholds for health risk assessment
RISK_THRESHOLDS = {
    "PM2.5": [
        (0,    12,   "LOW"),
        (12,   35,   "MEDIUM"),
        (35,   55,   "HIGH"),
        (55,   float("inf"), "CRITICAL"),
    ],
    "PM10": [
        (0,    54,   "LOW"),
        (54,   154,  "MEDIUM"),
        (154,  254,  "HIGH"),
        (254,  float("inf"), "CRITICAL"),
    ],
    "AR": [
        (0,    50,   "LOW"),
        (50,   100,  "MEDIUM"),
        (100,  150,  "HIGH"),
        (150,  float("inf"), "CRITICAL"),
    ],
    "RUIDO": [
        (0,    55,   "LOW"),
        (55,   70,   "MEDIUM"),
        (70,   85,   "HIGH"),
        (85,   float("inf"), "CRITICAL"),
    ],
    "TEMP": [
        (float("-inf"), 0,  "HIGH"),
        (0,    35,   "LOW"),
        (35,   40,   "MEDIUM"),
        (40,   float("inf"), "CRITICAL"),
    ],
}


def classify_risk(data_type: str, mean: float) -> str:
    thresholds = RISK_THRESHOLDS.get(data_type)
    if not thresholds:
        return "LOW"
    for low, high, level in thresholds:
        if low <= mean < high:
            return level
    return "LOW"


def detect_patterns(data_type: str, values: list[float]) -> list[str]:
    if not values:
        return []

    patterns = []
    ranges = {
        "PM2.5": (0, 500), "PM10": (0, 500), "AR": (0, 500),
        "RUIDO": (0, 150), "TEMP": (-50, 60), "HUM": (0, 100),
        "LUMINOSIDADE": (0, 150000),
    }
    max_range = ranges.get(data_type, (0, 100))[1]
    threshold_80 = max_range * 0.8

    high_count = sum(1 for v in values if v > threshold_80)
    if high_count > len(values) * 0.5:
        patterns.append(f"ALTA_CONCENTRACAO: mais de 50% das leituras acima de {threshold_80:.1f}")

    if len(values) >= 3:
        trend = values[-1] - values[0]
        if trend > max_range * 0.2:
            patterns.append("TENDENCIA_CRESCENTE: valores a aumentar significativamente")
        elif trend < -max_range * 0.2:
            patterns.append("TENDENCIA_DECRESCENTE: valores a diminuir significativamente")

    return patterns


class AnalysisServicer(analysis_pb2_grpc.AnalysisServiceServicer):

    def Analyze(self, request, context):
        print(f"[AnalysisService] Analyze: type={request.analysis_type} sensor={request.sensor_id} "
              f"data_type={request.data_type} points={len(request.data_points)}")

        values = [dp.value for dp in request.data_points]

        if not values:
            return analysis_pb2.AnalysisResult(
                analysis_type=request.analysis_type,
                risk_level="LOW",
                summary="Sem dados suficientes para análise",
                timestamp=datetime.now(timezone.utc).isoformat(),
            )

        mean_val   = statistics.mean(values)
        min_val    = min(values)
        max_val    = max(values)
        std_val    = statistics.stdev(values) if len(values) > 1 else 0.0

        risk_level = classify_risk(request.data_type, mean_val)
        patterns   = detect_patterns(request.data_type, values)

        analysis_type = request.analysis_type or "STATISTICS"

        if analysis_type == "HEALTH_RISK":
            summary = (
                f"Avaliação de risco para {request.data_type}: {risk_level}. "
                f"Média={mean_val:.2f}, Desvio={std_val:.2f}. "
                f"Baseado em {len(values)} medições."
            )
        elif analysis_type == "POLLUTION_PATTERN":
            summary = (
                f"Análise de padrões para {request.data_type}. "
                f"Padrões detectados: {len(patterns)}. "
                f"Min={min_val:.2f}, Max={max_val:.2f}, Média={mean_val:.2f}."
            )
        else:
            summary = (
                f"Estatísticas para {request.data_type}: "
                f"Média={mean_val:.2f}, Desvio={std_val:.2f}, "
                f"Min={min_val:.2f}, Max={max_val:.2f}. "
                f"Total de {len(values)} amostras."
            )

        result = analysis_pb2.AnalysisResult(
            analysis_type=analysis_type,
            mean=mean_val,
            std_dev=std_val,
            min_value=min_val,
            max_value=max_val,
            risk_level=risk_level,
            summary=summary,
            timestamp=datetime.now(timezone.utc).isoformat(),
        )
        result.patterns.extend(patterns)

        print(f"[AnalysisService] Result: risk={risk_level} mean={mean_val:.2f}")
        return result


def serve():
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    analysis_pb2_grpc.add_AnalysisServiceServicer_to_server(AnalysisServicer(), server)
    server.add_insecure_port("[::]:50052")
    server.start()
    print("=== ONE HEALTH ANALYSIS SERVICE (Python gRPC) ===")
    print("Porta: 50052")
    print("Aguardando chamadas RPC do Servidor...")
    server.wait_for_termination()


if __name__ == "__main__":
    serve()
