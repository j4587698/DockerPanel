import type { Terminal } from 'xterm'
import type { FitAddon } from '@xterm/addon-fit'

export type XtermTerminal = Terminal
export type XtermFitAddon = FitAddon

let terminalDepsPromise: Promise<{
  Terminal: typeof import('xterm').Terminal
  FitAddon: typeof import('@xterm/addon-fit').FitAddon
}> | null = null

export const loadTerminalDeps = () => {
  if (!terminalDepsPromise) {
    terminalDepsPromise = Promise.all([
      import('xterm'),
      import('@xterm/addon-fit'),
      import('xterm/css/xterm.css')
    ]).then(([xterm, fitAddon]) => ({
      Terminal: xterm.Terminal,
      FitAddon: fitAddon.FitAddon
    }))
  }

  return terminalDepsPromise
}