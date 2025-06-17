
#!/bin/bash

# Скрипт для развертывания приложения в Docker Swarm

echo "Building Docker image..."
docker build -t slow-light-requests-gate:latest .

echo "Initializing Docker Swarm (if not already initialized)..."
docker swarm init --advertise-addr 0.0.0.0 || echo "Swarm already initialized"

echo "Deploying stack to Docker Swarm..."
docker stack deploy -c docker-swarm-stack.yml slow-light-requests-gate-stack

echo "Checking services status..."
docker service ls

echo "Stack deployed successfully!"
echo "Application will be available on port 5000"
echo "RabbitMQ Management UI: http://localhost:15672 (guest/guest)"
echo "PostgreSQL: localhost:5432 (postgres/postgres123)"

echo ""
echo "Useful commands:"
echo "  docker service ls                                    - List services"
echo "  docker service logs slow-light-requests-gate-stack_app - View app logs"
echo "  docker stack rm slow-light-requests-gate-stack      - Remove stack"
echo "  docker service scale slow-light-requests-gate-stack_app=5 - Scale app to 5 replicas"
