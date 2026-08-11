FROM python:3.12-slim

WORKDIR /app

COPY requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt

COPY sourcebridge ./sourcebridge

ENV PORT=8790
EXPOSE 8790

ENTRYPOINT ["python", "-m", "sourcebridge"]
