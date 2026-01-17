#!/bin/bash
set -e

# Configuration
REGISTRY="reg.ht2.io"
IMAGE_NAME="castr"
TAG="${1:-latest}"  # Use first argument as tag, default to 'latest'
FULL_IMAGE="${REGISTRY}/${IMAGE_NAME}:${TAG}"

echo "=========================================="
echo "Building and Pushing Castr"
echo "Registry: ${REGISTRY}"
echo "Image: ${IMAGE_NAME}"
echo "Tag: ${TAG}"
echo "Full Image: ${FULL_IMAGE}"
echo "=========================================="

# Step 1: Build the Docker image
echo ""
echo "Step 1/3: Building Docker image..."
docker build -t "${FULL_IMAGE}" .

if [ $? -ne 0 ]; then
    echo "ERROR: Docker build failed!"
    exit 1
fi

echo "✓ Build completed successfully"

# Step 2: Push to registry
echo ""
echo "Step 2/3: Pushing to registry ${REGISTRY}..."
docker push "${FULL_IMAGE}"

if [ $? -ne 0 ]; then
    echo "ERROR: Docker push failed!"
    echo "Make sure you're logged in to the registry:"
    echo "  docker login ${REGISTRY}"
    exit 1
fi

echo "✓ Push completed successfully"

# Step 3: Delete local copy
echo ""
echo "Step 3/3: Deleting local image..."
docker rmi "${FULL_IMAGE}"

if [ $? -ne 0 ]; then
    echo "WARNING: Failed to delete local image"
    echo "You can manually remove it with:"
    echo "  docker rmi ${FULL_IMAGE}"
else
    echo "✓ Local image deleted successfully"
fi

# Summary
echo ""
echo "=========================================="
echo "✓ All steps completed successfully!"
echo "=========================================="
echo "Image available at: ${FULL_IMAGE}"
echo ""
echo "To pull and run:"
echo "  docker pull ${FULL_IMAGE}"
echo "  docker-compose up -d"
echo ""
