# Prova de conceito: libmpv + FFmpeg

O motor já foi promovido para `src\VisualSubSync.Media` (`Mpv\MpvMediaEngine`, `FFmpeg\FFmpegAudioExtractor`,
`FFmpeg\FFmpegKeyframeReader`). O que resta em `poc\` são os utilitários que exercitam esse motor.

| Projeto | Papel |
| --- | --- |
| `VisualSubSync.Poc.Bench` | `vss-medir-midia`: medições automáticas sem janela, gera um relatório Markdown |
| `VisualSubSync.Poc.Player` | `vss-poc-player`: janela WPF com vídeo, forma de onda e cursor, para avaliação à mão |

## Preparar no Windows

1. Baixar a libmpv (uma vez):
   ```powershell
   powershell -ExecutionPolicy Bypass -File poc\obter-libmpv.ps1
   ```
   O FFmpeg 7.1 vem pelo NuGet (`Sdcb.FFmpeg.runtime.windows-x64`) no primeiro `dotnet build`.
2. Compilar:
   ```powershell
   dotnet build poc\Poc.slnx
   ```

## Medir

```powershell
dotnet run --project poc\VisualSubSync.Poc.Bench -- "D:\Videos\episodio.mkv" --legenda "D:\Videos\episodio.ass"
```

Opções: `--buscas N` (padrão 200), `--trechos N` (padrão 20), `--saida arquivo.md`.
O relatório fica ao lado do vídeo (`episodio.medicao.md`). Vale rodar com AVI XviD, MKV H.264,
MP4 H.264 e HEVC, um vídeo de taxa variável e um de 23,976 fps.

O medidor roda o mpv sem vídeo e sem áudio (`vo=null`, `ao=null`); a latência real da saída de
som do Windows não entra nos números de parada. Isso se avalia no player.

## Avaliar à mão

```powershell
dotnet run --project poc\VisualSubSync.Poc.Player
```

1. Abrir um vídeo; a forma de onda é gerada pelo FFmpeg.
2. Arrastar na forma de onda para selecionar; "Tocar seleção" e "Loop".
3. Conferir se o cursor amarelo acompanha o som, inclusive a 60% de velocidade.
4. "Quadro inicial" e "Quadro final" mostram o quadro exato dos limites da seleção.
5. "Abrir legenda" com um `.ass` e conferir a renderização sobre o vídeo.
6. Redimensionar a janela e mudar a escala do Windows (125%, 150%).

Os resultados vão em `docs\PLANO-DE-MIGRACAO.md`, seção "Resultados da prova de conceito".
