# One-shot provisioning container for the OpenS4L stack (PostgreSQL):
# creates admin/admin and loads the free shop with correct client tabs.
# Run via `docker compose run --rm provision` (or `make provision`).
FROM python:3.12-slim
RUN pip install --no-cache-dir psycopg2-binary
COPY provision.py /provision.py
WORKDIR /
ENTRYPOINT ["python", "/provision.py"]
