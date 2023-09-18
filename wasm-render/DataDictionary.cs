// This is a quick reimplementation of VRChat's Data types.

using System;
using System.Collections;
using System.Collections.Generic;

public enum TokenType
{
    Null,
    String,
    DataList,
    DataDictionary,
    Double,
    Boolean,
}

public struct DataToken
{
    public TokenType TokenType { get; init; }
    DataDictionary dictionary;
    DataList list;
    double d;
    string s;
    bool b;

    public DataDictionary DataDictionary
    {
        get
        {
            AssertType(TokenType.DataDictionary);
            return this.dictionary;
        }
    }

    public DataList DataList
    {
        get
        {
            AssertType(TokenType.DataList);
            return this.list;
        }
    }

    public double Double
    {
        get
        {
            AssertType(TokenType.Double);
            return this.d;
        }
    }

    public string String
    {
        get
        {
            AssertType(TokenType.String);
            return this.s;
        }
    }

    public bool Boolean
    {
        get
        {
            AssertType(TokenType.Boolean);
            return this.b;
        }
    }

    void AssertType(TokenType expected)
    {
        if (this.TokenType != expected)
        {
            throw new InvalidCastException($"Wanted {expected} but had {this.TokenType}");
        }
    }

    public static implicit operator DataToken(DataDictionary v) => new DataToken {TokenType = TokenType.DataDictionary, dictionary = v};
    public static implicit operator DataToken(DataList v) => new DataToken {TokenType = TokenType.DataList, list = v};
    public static implicit operator DataToken(double v) => new DataToken {TokenType = TokenType.Double, d = v};
    public static implicit operator DataToken(string v) => new DataToken {TokenType = TokenType.String, s = v};
    public static implicit operator DataToken(bool v) => new DataToken {TokenType = TokenType.Boolean, b = v};
}

public class DataDictionary: IEnumerable<KeyValuePair<DataToken, DataToken>>
{
    readonly Dictionary<DataToken, DataToken> content = new Dictionary<DataToken, DataToken>();

    public DataToken this[DataToken k]
    {
        get
        {
            return content[k];
        }
        set
        {
            content[k] = value;
        }
    }

    public bool ContainsKey(DataToken key)
    {
        return content.ContainsKey(key);
    }

    public IEnumerator<KeyValuePair<DataToken, DataToken>> GetEnumerator()
    {
        return ((IEnumerable<KeyValuePair<DataToken, DataToken>>)content).GetEnumerator();
    }

    public DataDictionary ShallowClone()
    {
        var clone = new DataDictionary();
        foreach (var p in content)
        {
            clone[p.Key] = p.Value;
        }
        return clone;
    }

    public bool TryGetValue(DataToken key, TokenType t, out DataToken token)
    {
        token = default;
        if (!content.TryGetValue(key, out var temp))
        {
            return false;
        }
        if (temp.TokenType == t)
        {
            token = temp;
            return true;
        }
        return false;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)content).GetEnumerator();
    }
}

public class DataList: IEnumerable<DataToken>
{
    readonly List<DataToken> content = new List<DataToken>();

    public int Count => content.Count;

    public bool TryGetValue(int i, TokenType t, out DataToken token)
    {
        token = default;
        if (i < 0 || content.Count <= i)
        {
            return false;
        }
        var temp = content[i];
        if (temp.TokenType == t)
        {
            token = temp;
            return true;
        }
        return false;
    }

    public bool Contains(DataToken token)
    {
        return content.Contains(token);
    }

    internal void Add(DataToken occurrence)
    {
        content.Add(occurrence);
    }

    public IEnumerator<DataToken> GetEnumerator()
    {
        return ((IEnumerable<DataToken>)content).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)content).GetEnumerator();
    }
}
