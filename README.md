# 1️⃣🐝🏎️ The One Billion Row Challenge -- C# Edition -- nietras

* See https://github.com/gunnarmorling/1brc for original Java challenge.
* See https://huggingface.co/datasets/nietras/1brc.data for data only.

For command line process start/stop measurement
https://github.com/sharkdp/hyperfine is used. This can be installed with
Chocolatey (or WinGet):
```
choco install hyperfine
```

![1Brc Comparison](1brc-comparison.png)

## Data Statistics for One Billion Row Challenge
1BRC numbers for 1B rows data file. 413 station names. Length = Unique Count.
```
 3 =  2
 4 = 18
 5 = 42
 6 = 87
 7 = 66
 8 = 57
 9 = 47
10 = 26
11 = 26
12 = 17
13 =  9
14 =  4
15 =  2
16 =  6
17 =  1
18 =  1
24 =  1
26 =  1
```
Note how all lie within first 32 bytes. Optimise for that. Only 4/413 are longer
than 16 bytes, so 1% only. Optimize for that. Keep in mind length=16 cannot be
found by Vector128. In any case, doing one Vector256 search is faster than two
Vector128 searches. 32 - 26 = 6. Longest number is `-99.9` or 5 bytes so most
line endings will be found within this too. Optimize for that.
Handle remaining 1% with a fallback still fast but not as fast.

⚠ An implementation should not only handle the above and the specific station
names, but pass a set of tests that cover all possible uses. Hence, above
statistics are only used for optimizing the hot path not to only handle that.

[BrcTest.cs](./src/Brc.Test/BrcTest.cs) runs and passes all the tests specified
by the challenge for this C#/.NET 8 version.