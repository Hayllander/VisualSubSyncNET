# Comparação de paridade — 25/10/2026

Escopo: mudanças feitas em `C:\Projetos\VisualSubSync.NET` depois de 23/10/2026 (commits `2ebdab0`,
`fbacfd8` e `1a6822d` e as alterações locais), comparadas com os fontes do 1.0.1
(`C:\Projetos\VisualSubSync`) e do Enhanced (`C:\Projetos\VisualSubSyncEnh`). As correções de 23/10
(`docs/auditoria-2026-10-23/`) foram conferidas e continuam no código.

## Conferem com os fontes

| Mudança no .NET | Fonte conferida |
| --- | --- |
| `[Fonts] SubVideo`, `TransparencyColorSubVideo`, `OutlineSubVideo`, `OutlineColorSubVideo`, `TransparencyOutlineColorSubVideo`, `ShadowSubVideo`, `ShadowColorSubVideo`, `TransparencyShadowColorSubVideo`: nomes, padrões (`Arial,18,1,0,clWhite`, 0, 2, clBlack, 0, 3, clBlack, 4) e escala 0..10 | Enh `PreferencesFormUnit.pas` (SetDefault, LoadIni, SaveIni) e `Renderer.pas` (`Trunc(x / 10 * $FF)`) |
| Botão Default de "Subtitles video" | Enh `PreferencesFormUnit.pas`, `ButtonDefaultSubtitlesVideoClick` |
| A seleção acompanha a legenda quando um plugin muda Start/Stop | 1.0.1 `main.pas`, `OnSubtitleRangeJSWrapperChangeStart/Stop` |
| Duplo clique na borda tracejada chega aos plugins | 1.0.1 `WAVDisplayerUnit.pas`, `TWAVDisplayer.DblClick` (usa `FDynamicSelRange`/`FDynamicEditMode` da detecção do mouse) |
| Check for updates | Enh `main.pas` (`CheckUpdates`, `TimerChkUpdatesTimer`); diferenças já registradas no plano |

## Divergências corrigidas

### Projetos SSA/ASS perdiam os próprios estilos no vídeo
- Fonte: Enh `main.pas`, `UpdateSubtitleForPreview`, grava a legenda no formato do projeto. Enh `Renderer.pas`,
  `TDShowRenderer.SetSubtitleFont`, passa a fonte ao VSFilter com `put_TextSettings`/`put_TextSettings2`, que valem
  só para legendas sem estilos.
- .NET antes: o preview sempre virava um `.ass` em que todas as legendas usavam o estilo Default das preferências.
- Agora: SSA/ASS são gravados no próprio formato, com os estilos do arquivo; só o SRT vira um ASS com o estilo de
  "Subtitles video" (`MainViewModel.UpdateSubtitleForPreview`).

### Controles de "Subtitles video" ativos com projeto aberto
- Fonte: Enh `main.pas`, `ShowPreferences`: `EditSubVideoFont`, `bttSubVideoFont`, as três barras de transparência,
  contorno, sombra, as duas cores e o botão Default recebem `Enabled := not ActionClose.Enabled`.
- Agora: `IDialogService.ShowPreferences` recebe `projectOpen` e a `PreferencesWindow` desabilita esses controles.

## Observações sem alteração

- `MainViewModel.ApplyVideoSubtitleStyle()` não faz nada, e `MpvMediaEngine.SetSubtitleStyle`/`BuildAssForceStyle`
  nunca são chamados: é código morto.
- `ShellAssociation` ainda grava `VisualSubSync` como nome no registro. Com o 1.0.1 instalado na mesma máquina, uma
  associação sobrescreve a outra.
- A pasta temporária (`SubtitlePreview`, `PreferencesWindow`) e a pasta de dados (`StoragePaths`) continuam com o
  nome `VisualSubSync`.

Build sem avisos; 222 testes do Core passam (os 2 de `StorageSafetyTests` só rodam no Windows).
