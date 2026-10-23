# Formulários do VisualSubSync Enhanced

Texto dos 22 formulários (DFM) gravados como recurso em `C:\Projetos\VisualSubSyncEnh\VisualSubSyncEnh.exe`
(Visualsubsync Enhanced 1.2.29, de Gilberto Beda). Estes arquivos dão layout, textos, dicas,
atalhos e ícones do executável de referência. O comportamento-base vem do código 1.0.1 em
`C:\Projetos\VisualSubSync`.

Em 21/10/2026 o usuário forneceu os fontes do Enhanced **1.2.33.0**, em
`C:\Projetos\VisualSubSyncEnh\fontes` (versão registrada em `VisualSubSyncEnh.dof`). Eles passam
a permitir comparação do comportamento interno, inclusive de recursos adicionais. Não se deve
confundir essa versão com o executável **1.2.29** de onde estes DFM foram extraídos.

A comparação está registrada em `../AUDITORIA-PARIDADE-2026-10-21.md` e no JSON de
rastreabilidade gerado por `tools/audit-parity.py`. Os fontes Delphi não foram alterados.

- `TMAINFORM.txt` e os demais: propriedades de cada componente. Valores binários (glifos, listas de imagens)
  foram extraídos para `src\VisualSubSync.App\Assets`.
- `MENU.txt`: árvore do menu principal com ação, atalho e índice de ícone.

Os atalhos efetivos podem ser trocados pelo usuário na seção `[Hotkeys]` do `VisualSubSync.ini`.
