# VisualSubSync.NET: regras do projeto

## Fidelidade ao original (diretriz estável)

Tudo o que for possível deve ficar igual ao VisualSubSync original: interface, layout, textos,
atalhos, cores, comportamento, fluxo e formatos de arquivo. As referências, nesta ordem, são:

1. Interface (layout, menus, textos, dicas, ícones): o VisualSubSync Enhanced instalado em
   `C:\Projetos\VisualSubSyncEnh`, que é a versão usada no dia a dia. Os formulários dele estão
   extraídos em `docs\enhanced\`.
2. Comportamento: o código original em `C:\Projetos\VisualSubSync` (`main.dfm`, `main.pas`,
   `WAVDisplayerUnit.pas`, demais `*.dfm`/`*.pas`). O Enhanced mudou pouco em relação a ele.
   Nenhuma dessas pastas é alterada.
3. `docs\INVENTARIO.md`.
4. `docs\PLANO-DE-MIGRACAO.md`.

Qualquer diferença deliberada precisa ser pequena, justificada e registrada em
`docs\PLANO-DE-MIGRACAO.md` (seção de diferenças). Funções ainda não portadas aparecem no lugar
e com o texto do original, desabilitadas, em vez de omitidas.

## Outras regras

- Trabalho local em `C:\Projetos`; nada sobe para a nuvem (sem GitHub).
- Conversas e documentação em português; textos da interface em inglês, como no original.
- .NET 10, WPF, MVVM com CommunityToolkit.Mvvm; avisos tratados como erro.
