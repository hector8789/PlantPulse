import { useState } from 'react'
import { Button, Card, EmptyState, Table, TBody, Td, Th, THead, Tr } from '@/components/ui'
import { formatDateTime, formatInt } from '@/lib/format'
import type { TurnoLinea } from '../types'
import { CorregirProduccionForm } from './CorregirProduccionForm'
import { RegistroProduccionForm } from './RegistroProduccionForm'

export function ProduccionSection({ turno, editable, zona }: { turno: TurnoLinea; editable: boolean; zona: string }) {
  const [editandoId, setEditandoId] = useState<string | null>(null)
  const editando = editable ? turno.registros.find((r) => r.id === editandoId) : undefined

  return (
    <Card title="Producción" description="Una captura por orden en el turno; para cambiarla usa Corregir.">
      <div className="flex flex-col gap-4">
        {turno.registros.length === 0 ? (
          <EmptyState title="Sin producción registrada" />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Orden</Th>
                <Th>Material</Th>
                <Th align="right">Ciclo ideal</Th>
                <Th align="right">Totales</Th>
                <Th align="right">Buenas</Th>
                <Th align="right">Scrap</Th>
                <Th>Registrado</Th>
                {editable && (
                  <Th>
                    <span className="sr-only">Acciones</span>
                  </Th>
                )}
              </tr>
            </THead>
            <TBody>
              {turno.registros.map((r) => (
                <Tr key={r.id}>
                  <Td className="font-medium">{r.ordenNumero}</Td>
                  <Td>{r.materialCodigo}</Td>
                  <Td align="right">{r.tiempoCicloIdealSeg} s</Td>
                  <Td align="right">{formatInt(r.piezasTotales)}</Td>
                  <Td align="right">{formatInt(r.piezasBuenas)}</Td>
                  <Td align="right" className={r.scrap > 0 ? 'text-red-700' : undefined}>
                    {formatInt(r.scrap)}
                  </Td>
                  <Td className="text-slate-600">
                    {formatDateTime(r.modificadoEnUtc ?? r.registradoEnUtc, zona)}
                    {r.modificadoEnUtc && <span className="ml-1 text-xs text-amber-700">(corregido)</span>}
                  </Td>
                  {editable && (
                    <Td align="right">
                      <Button size="sm" variant="ghost" onClick={() => setEditandoId(r.id)}>
                        Corregir
                      </Button>
                    </Td>
                  )}
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
        {editando && (
          <CorregirProduccionForm
            key={editando.id}
            turnoId={turno.id}
            registro={editando}
            onDone={() => setEditandoId(null)}
          />
        )}
        {/* key: tras registrar se monta limpio (los campos numéricos no tienen valor vacío en el schema) */}
        {editable && <RegistroProduccionForm key={turno.registros.length} turno={turno} />}
      </div>
    </Card>
  )
}
