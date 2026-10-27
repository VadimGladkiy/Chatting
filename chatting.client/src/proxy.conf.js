const { env } = require('process');

const target = env.API_PROXY_TARGET || (env.ASPNETCORE_HTTPS_PORT ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}` :
  env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'http://localhost:5111');

const PROXY_CONFIG = [
  {
    context: [
      "/api",
    ],
    target,
    secure: false
  },
  {
    context: ['/ws'],
    target,
    secure: false,
    ws: true
  }
]

module.exports = PROXY_CONFIG;
