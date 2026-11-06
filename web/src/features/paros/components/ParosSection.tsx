import { Badge, Card, ConfirmButton, EmptyState, Table, TBody, Td, Th, THead, Tr } from '@/components/ui'
import type { TurnoLinea } from '@/features/produccion/types'
import { formatInt, formatTime } from '@/lib/format'
import { TIPO_PARO_LABEL } from '@/types/domain'
import { useEliminarParo } from '../api/useParos'
import { ParoForm } from './ParoForm'

export function ParosSection({ turno, editable, zona }: { turno: TurnoLinea; editable: boolean; zona: string }) {
  const eliminar = useEliminarParo(turno.id)
  const paros = [...turno.paros].sort((a, b) => a.inicioUtc.localeCompare(b.inicioUtc))
  const totalMin = paros.reduce((s, p) => s + p.duracionMin, 0)

  return (
    <Card
      title="Paros"
      description={`Deben caer completos dentro del turno (${formatTime(turno.inicioUtc, zona)}–${formatTime(turno.finUtc, zona)}). Si un paro cruza de turno, captúralo como dos.`}
    >
      <div className="flex flex-col gap-4">
        {paros.length === 0 ? (
          <EmptyState title="Sin paros registrados" />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Causa</Th>
                <Th>Tipo</Th>
                <Th>Inicio</Th>
                <Th>Fin</Th>
                <Th align="right">Duración</Th>
                <Th>Comentario</Th>
                {editable && (
                  <Th>
                    <span className="sr-only">Acciones</span>
                  </Th>
                )}
              </tr>
            </THead>
            <TBody>
              {paros.map((p) => (
                <Tr key={p.id}>
                  <Td>
                    <span className="font-medium">{p.causaCodigo}</span>{' '}
                    <span className="text-slate-600">{p.causaDescripcion}</span>
                  </Td>
                  <Td>
                    <Badge tone={p.tipo === 'Planificado' ? 'neutral' : 'red'}>{TIPO_PARO_LABEL[p.tipo]}</Badge>
                  </Td>
                  <Td>{formatTime(p.inicioUtc, zona)}</Td>
                  <Td>{formatTime(p.finUtc, zona)}</Td>
                  <Td align="right">{formatInt(p.duracionMin)} min</Td>
                  <Td className="max-w-xs truncate whitespace-normal text-slate-600" title={p.comentario ?? undefined}>
                    {p.comentario ?? '—'}
                  </Td>
                  {editable && (
                    <Td align="right">
                      <ConfirmButton
                        variant="danger"
                        pregunta="¿Eliminar?"
                        confirmarLabel="Eliminar"
                        loading={eliminar.isPending && eliminar.variables === p.id}
                        onConfirm={() => eliminar.mutate(p.id)}
                      >
                        Eliminar
                      </ConfirmButton>
                    </Td>
                  )}
                </Tr>
              ))}
              <tr className="bg-slate-50 font-medium">
                <Td colSpan={4}>Total</Td>
                <Td align="right">{formatInt(totalMin)} min</Td>
                <Td colSpan={editable ? 2 : 1} />
              </tr>
            </TBody>
          </Table>
        )}
        {editable && <ParoForm turno={turno} zona={zona} />}
      </div>
    </Card>
  )
}
