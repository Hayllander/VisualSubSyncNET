# Medição de mídia: ntsc.mp4

- Data: 2026-10-18 19:15
- Sistema: Ubuntu 24.04.5 LTS
- Duração: 60023 ms, 23.976 fps, 640×360
- Abertura no mpv: 16 ms

## Áudio e quadros-chave (FFmpeg)

- Picos: 6001 em 148 ms (44100 Hz, 1 canal(is), 441 amostras por pico)
- Duração pelos picos: 60000 ms (diferença para o mpv: -23 ms)
- `.peak` gravado e relido igual: sim
- Quadros-chave: 30 em 16 ms; primeiros: 0, 2002, 4004, 6006, 8008, 10010

## Busca exata (100 posições aleatórias)

| Medida | p50 | p95 | máx |
| --- | --- | --- | --- |
| Erro do quadro exibido (ms) | 15.7 | 35 | 36.5 |
| Latência da busca (ms) | 17.4 | 28.9 | 33.7 |

- Dentro de 1 quadro (41.7 ms): 100%

## Tocar trecho (10 trechos de 1 a 3 s)

| Medida | p50 | p95 | máx |
| --- | --- | --- | --- |
| Posição ao detectar o fim − fim (ms) | 0 | 21 | 21 |
| Posição final após pausar − fim (ms) | 0.4 | 21 | 21 |
| Atualizações de posição por segundo | 24.7 | 25.3 | 25.3 |

## Velocidade

- 60 %: 2000 ms de mídia em 3300 ms (esperado ≈ 3333)
- 80 %: 2000 ms de mídia em 2505 ms (esperado ≈ 2500)
- 100 %: 2000 ms de mídia em 2011 ms (esperado ≈ 2000)

