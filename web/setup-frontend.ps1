# ============================================================
#  PlantPulse Web - comandos con los que se creó este proyecto
#  (de referencia: si usas el zip, solo necesitas "npm install")
#  Requiere Node 20.19+ o 22.12+
# ============================================================
npm create vite@latest plantpulse-web -- --template react-ts --no-interactive
Set-Location plantpulse-web
npm install
npm install @tanstack/react-query axios zustand react-hook-form zod @hookform/resolvers powerbi-client powerbi-client-react
npm install -D tailwindcss @tailwindcss/vite
