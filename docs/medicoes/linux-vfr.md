# Medição de mídia: vfr.mkv

- Data: 2026-10-18 19:16
- Sistema: Ubuntu 24.04.5 LTS
- Duração: 60021 ms, 0 fps, 640×360
- Abertura no mpv: 16 ms

## Áudio e quadros-chave (FFmpeg)

- Picos: 6004 em 195 ms (48000 Hz, 1 canal(is), 480 amostras por pico)
- Duração pelos picos: 60032 ms (diferença para o mpv: 11 ms)
- `.peak` gravado e relido igual: sim
- Quadros-chave: 45 em 18 ms; primeiros: 21, 2021, 4021, 6021, 8021, 10021

## Busca exata (100 posições aleatórias)

| Medida | p50 | p95 | máx |
| --- | --- | --- | --- |
| Erro do quadro exibido (ms) | 9 | 31 | 34 |
| Latência da busca (ms) | 16.8 | 30.7 | 38 |

- Dentro de 1 quadro (40 ms): 100%

## Tocar trecho (10 trechos de 1 a 3 s)

| Medida | p50 | p95 | máx |
| --- | --- | --- | --- |
| Posição ao detectar o fim − fim (ms) | 10 | 39 | 39 |
| Posição final após pausar − fim (ms) | 10 | 39 | 39 |
| Atualizações de posição por segundo | 25.7 | 51.2 | 51.2 |

## Velocidade

- 60 %: 2000 ms de mídia em 3299 ms (esperado ≈ 3333)
- 80 %: 2000 ms de mídia em 2467 ms (esperado ≈ 2500)
- 100 %: 2000 ms de mídia em 1985 ms (esperado ≈ 2000)

