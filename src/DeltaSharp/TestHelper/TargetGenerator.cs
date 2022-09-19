using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeltaSharp;


internal class TargetGenerator
{
    public byte[] Source => Encoding.UTF8.GetBytes(string.Join("\n", SourceLines) + "\n");

    public List<string> SourceLines { get; private set; }

    Random _random;

    public TargetGenerator(string? source = null, Random? random=null)
    {
        _random = random ?? new Random();

        if (source is not null)
        {            
            var lines = source.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
            SourceLines = lines;
        }
        else
        {
            SourceLines = new List<string>();
        }       
    }

    public TargetGenerator(IEnumerable<string> lines, Random? random = null)
    {
        _random = random ?? new Random();

        SourceLines = lines.ToList();
    }

    public void Execute(string input)
    {
        var lines = input.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        
        lines = lines.SelectMany(l => l.Split(';')).Select(l => l.Trim()).ToArray();
        
        var target = new List<string>(SourceLines);

        var operations = TargetGeneratorOperation.Parse(lines);

        Execute(operations);

    }

    public void Execute(TargetGeneratorOperation[] operations)
    {
        var target = SourceLines;

        foreach (var op in operations)
            op.Apply(target,_random);

    } 

}

internal enum OperationEnum
{
    Set,
    Add,
    Del,
    Swp
}

internal class TargetGeneratorOperation
{
    public OperationEnum Op { get; set; }

    public string[]? Args { get; set; }


    private TargetGeneratorOperation()
    {
    }    

    public void Apply(List<string> source,Random random)
    {
        switch (Op)
        {
            case OperationEnum.Add: ApplyAdd(source, random); break;
            case OperationEnum.Del: ApplyDel(source); break;
            case OperationEnum.Set: ApplySet(source, random); break;
            case OperationEnum.Swp: ApplySwap(source); break;
        }
    }
    
    private void ApplyAdd(List<string> source,Random random)
    {
        if (Args.Length <1)
            throw new InvalidDataException("[ApplyAdd] invalid argument count");

        if (!int.TryParse(Args[0],out var position))
            throw new InvalidDataException("[ApplyAdd] invalid position");

        if (position > source.Count - 1 || position == -1)
            position = source.Count;

        Span<byte> buffer = stackalloc byte[16];

        var count = 1;
        var data = string.Empty;
        if (Args.Length > 1)
        {
            data = Args[1];

            if (Args.Length > 2)
            {
                if (!int.TryParse(Args[2], out count))
                    throw new InvalidDataException("[ApplySet] invalid count");
            }
        }
        else
        {
            random.NextBytes(buffer);

            data = Convert.ToBase64String(buffer, Base64FormattingOptions.None)
                    .Replace("==", "").Replace("=", "");
        }
        

        while (count > 0)
        {
            var s = data;
            if (data == "RND")
            {                
                random.NextBytes(buffer);

                s = Convert.ToBase64String(buffer, Base64FormattingOptions.None)
                    .Replace("==", "").Replace("=", "");

            }                
            else if (data.StartsWith("RND(") && data.EndsWith(")"))
            {
                var numberString = data.Replace("RND(", "").Replace(")", "");
                var randCount = int.Parse(numberString);

                var tmp = new byte[randCount];
                for (var i = 0; i < tmp.Length; i++)
                    tmp[i] = (byte)random.Next(32, 127);

                s = Encoding.UTF8.GetString(tmp).Replace('\r', 'r').Replace('\n', 'n');
            }

            source.Insert(position, s);
            count--;
        }

    }

    private void ApplySet(List<string> source,Random random)
    {
        if (Args.Length < 1)
            throw new InvalidDataException("[ApplySet] invalid argument count");

        if (!int.TryParse(Args[0], out var position))
            throw new InvalidDataException("[ApplySet] invalid position");

        if(position>source.Count-1)
            throw new InvalidDataException("[ApplySet] invalid position");

        Span<byte> buffer = stackalloc byte[16];

        int count = 1;
        int step = 1;
        var data = string.Empty;
        
        if (Args.Length > 1)
        {
            data = Args[1];

            if (Args.Length > 2)
            {
                if (!int.TryParse(Args[2], out count))
                    throw new InvalidDataException("[ApplySet] invalid count");
            }

            if (Args.Length > 3)
            {
                if (!int.TryParse(Args[3], out step))
                    throw new InvalidDataException("[ApplySet] invalid step");
            }
        }
        else
        {
           
            random.NextBytes(buffer);

            data = Convert.ToBase64String(buffer, Base64FormattingOptions.None)
                    .Replace("==", "").Replace("=", "");
        }

        while(count > 0)
        {
            var i = position + (count - 1) * step;
            
            var s = data;
            if (data == "RND")
            {
                random.NextBytes(buffer);
                s = Convert.ToBase64String(buffer, Base64FormattingOptions.None)
                   .Replace("==", "").Replace("=", "");
            }               
            else if (data.StartsWith("RND(") && data.EndsWith(")"))
            {
                var numberString = data.Replace("RND(", "").Replace(")", "");
                var randCount = int.Parse(numberString);

                var tmp = new byte[randCount];
                for (var z = 0; z < tmp.Length; z++)
                    tmp[z] = (byte)random.Next(32, 127);

                s = Encoding.UTF8.GetString(tmp).Replace('\r', 'r').Replace('\n', 'n');
            }
            else if (data == "HASH")
            {
                var current = source[i];
                s = GetHashed(current);
            }

            source[i] = s;

            count--;
        }        
    }

    private void ApplyDel(List<string> source)
    {
        if (Args.Length < 1)
            throw new InvalidDataException("[ApplyDel] invalid argument count");

        if (!int.TryParse(Args[0], out var position))
            throw new InvalidDataException("[ApplyDel] invalid position");

        var count = 1;
        if (Args.Length > 1)
        {
            if (!int.TryParse(Args[1], out count))
                throw new InvalidDataException("[ApplyDel] invalid count");
        }

        source.RemoveRange(position, count);
    }

    private void ApplySwap(List<string> source)
    {
        if (Args.Length < 2)
            throw new InvalidDataException("[ApplySwap] invalid argument count");

        if (!int.TryParse(Args[0], out var n1))
            throw new InvalidDataException("[ApplySwap] invalid position 1");

        if (!int.TryParse(Args[1], out var n2))
            throw new InvalidDataException("[ApplySwap] invalid position 2");

        int n3 = 1;
        if(Args.Length > 2)
        {
            //swap block

            if (!int.TryParse(Args[2], out n3))
                throw new InvalidDataException("[ApplySwap] invalid position 2");

        }
        if (n2 + n3 > source.Count || n1 + n3 > source.Count)
            throw new InvalidDataException("[ApplySwap] invalid range");

        foreach (var i in Enumerable.Range(0, n3))
        {
            var s = source[n1 + i];
            var s2 = source[n2 + i];
            source[n1 + i] = s2;
            source[n2 + i] = s;
        }
        
    }

    private string GetHashed(string source)
    {
        var sb = new StringBuilder();

        var skip = 0;
        while (sb.Length < source.Length)
        {
            var s = source.Substring(skip, 16);
            var hash = System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(s));
            var hashString = Convert.ToBase64String(hash).Replace("==", "").Replace("=", "");
            sb.Append(hashString);

            skip++;
        }

        return sb.ToString().Substring(0, source.Length);
    }

    public static TargetGeneratorOperation ParseOperation(string s)
    {
        var opString = s.Substring(0, 3);
        if (!Enum.TryParse<OperationEnum>(opString, ignoreCase: true, out var op))
            throw new InvalidDataException($"invalid operation {opString}");

        var args = s.Split(' ').Skip(1);

        return new TargetGeneratorOperation()
        {
            Op = op,
            Args = args.ToArray()
        };
    }

    public static TargetGeneratorOperation[] Parse(string[] lines)
    {

        var operations = new List<TargetGeneratorOperation>();

        foreach(var line in lines)
            operations.Add(ParseOperation(line));

        return operations.ToArray();
    }
}

