export interface NavItem {
  to: string
  label: string
  icon: string
}

export const NAV_ITEMS: readonly NavItem[] = [
  { to: '/oee', label: 'OEE', icon: '◔' },
  { to: '/turnos', label: 'Turnos y captura', icon: '▤' },
  { to: '/reportes', label: 'Reportes Power BI', icon: '▦' },
]
