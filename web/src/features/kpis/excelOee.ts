import type { ColumnaExcel } from '@/lib/excel'
import type { Oee } from '@/types/domain'

/** Columnas del desglose de OEE, reutilizables en cualquier fila que tenga `oee`. */
export function columnasOee<T>(oeeDe: (fila: T) => Oee): ColumnaExcel<T>[] {
  return [
    { header: 'OEE', valor: (f) => oeeDe(f).oee, formato: 'porcentaje' },
    { header: 'Disponibilidad', valor: (f) => oeeDe(f).disponibilidad, formato: 'porcentaje' },
    { header: 'Desempeño', valor: (f) => oeeDe(f).desempeno, formato: 'porcentaje' },
    { header: 'Calidad', valor: (f) => oeeDe(f).calidad, formato: 'porcentaje' },
    { header: 'Piezas totales', valor: (f) => oeeDe(f).piezasTotales, formato: 'entero' },
    { header: 'Piezas buenas', valor: (f) => oeeDe(f).piezasBuenas, formato: 'entero' },
    { header: 'Scrap (pzs)', valor: (f) => oeeDe(f).scrap, formato: 'entero' },
    { header: 'Tiempo planificado (min)', valor: (f) => oeeDe(f).tiempoPlanificadoMin, formato: 'decimal' },
    { header: 'Tiempo operativo (min)', valor: (f) => oeeDe(f).tiempoOperativoMin, formato: 'decimal' },
    { header: 'Paros planificados (min)', valor: (f) => oeeDe(f).parosPlanificadosMin, formato: 'decimal' },
    { header: 'Paros no planificados (min)', valor: (f) => oeeDe(f).parosNoPlanificadosMin, formato: 'decimal' },
    { header: 'Desempeño excedido', valor: (f) => (oeeDe(f).desempenoExcedido ? 'Sí' : 'No') },
  ]
}
