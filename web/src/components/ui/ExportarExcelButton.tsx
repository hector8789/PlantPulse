import { useState } from 'react'
import { exportarExcel, type HojaExcel } from '@/lib/excel'
import { toast } from '@/stores/toastStore'
import { Button } from './Button'

interface Props {
  nombreArchivo: string
  /** Se llama al hacer clic, para armar las hojas solo cuando se necesitan */
  hojas: () => HojaExcel[]
  disabled?: boolean
}

export function ExportarExcelButton({ nombreArchivo, hojas, disabled }: Props) {
  const [exportando, setExportando] = useState(false)
  return (
    <Button
      variant="secondary"
      loading={exportando}
      disabled={disabled}
      onClick={async () => {
        setExportando(true)
        try {
          await exportarExcel(nombreArchivo, hojas())
        } catch {
          toast.error('No se pudo exportar', 'Intenta de nuevo; si persiste, recarga la página.')
        } finally {
          setExportando(false)
        }
      }}
    >
      Exportar a Excel
    </Button>
  )
}
