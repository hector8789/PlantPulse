import { Badge } from '@/components/ui'
import type { EstadoTurno } from '@/types/domain'

export function EstadoTurnoBadge({ estado }: { estado: EstadoTurno }) {
  return <Badge tone={estado === 'Abierto' ? 'blue' : 'neutral'}>{estado}</Badge>
}
