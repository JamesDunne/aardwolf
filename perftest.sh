#!/bin/sh

# 10k connections:
httperf --num-conns=1000 --num-calls=100 --server=localhost --port=80 --uri=/foo
