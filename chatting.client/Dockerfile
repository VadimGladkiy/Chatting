FROM node:22-bookworm-slim

WORKDIR /app

COPY chatting.client/package.json chatting.client/package-lock.json ./
RUN npm ci

COPY chatting.client/ ./

EXPOSE 53160

CMD ["npx", "ng", "serve", "--host", "0.0.0.0", "--port", "53160"]
