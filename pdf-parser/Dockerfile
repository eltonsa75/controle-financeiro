
# ==========================================
# COMPRAS INTELIGENTES
# Python / FastAPI - Produção
# ==========================================

FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1

WORKDIR /app

# Dependências necessárias para o processamento
# de documentos PDF
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    build-essential && \
    rm -rf /var/lib/apt/lists/*

# Instala as bibliotecas Python
COPY requirements.txt .

RUN pip install --no-cache-dir -r requirements.txt

# Copia os arquivos da aplicação
COPY . .

# Porta interna do serviço FastAPI
EXPOSE 8081

# Inicializa a API Python
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8081"]
