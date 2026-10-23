# Medição de mídia: cfr25.mkv

- Data: 2026-10-18 19:14
- Sistema: Ubuntu 24.04.5 LTS
- Duração: 60021 ms, 25 fps, 640×360
- Abertura no mpv: 17 ms

## Áudio e quadros-chave (FFmpeg)

- Picos: 6004 em 241 ms (48000 Hz, 2 canal(is), 480 amostras por pico)
- Duração pelos picos: 60032 ms (diferença para o mpv: 11 ms)
- `.peak` gravado e relido igual: sim
- Quadros-chave: 30 em 14 ms; primeiros: 21, 2021, 4021, 6021, 8021, 10021

## Busca exata (100 posições aleatórias)

| Medida | p50 | p95 | máx |
| --- | --- | --- | --- |
| Erro do quadro exibido (ms) | 13 | 33 | 34 |
| Latência da busca (ms) | 18.7 | 34.5 | 40.9 |

- Dentro de 1 quadro (40 ms): 100%

## Tocar trecho (10 trechos de 1 a 3 s)

| Medida | p50 | p95 | máx |
| --- | --- | --- | --- |
| Posição ao detectar o fim − fim (ms) | -10 | 19 | 19 |
| Posição final após pausar − fim (ms) | -10 | 19 | 19 |
| Atualizações de posição por segundo | 25.6 | 26.3 | 26.3 |

## Velocidade

- 60 %: 2000 ms de mídia em 3303 ms (esperado ≈ 3333)
- 80 %: 2000 ms de mídia em 2502 ms (esperado ≈ 2500)
- 100 %: 2000 ms de mídia em 2013 ms (esperado ≈ 2000)

## Legenda

- `t.ass` carregada e recarregada sem erro.

