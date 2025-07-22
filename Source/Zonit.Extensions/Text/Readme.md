# Zonit.Extensions.Text

A comprehensive text analysis and processing library providing utilities for text counting, readability analysis, and content evaluation. This library helps developers analyze text content with detailed statistics and readability metrics.

## Features

1. **Text Counting and Statistics**  
   Count characters, words, sentences, paragraphs, and other text elements with high accuracy.

2. **Readability Analysis**  
   Calculate readability scores using the Flesch Reading Ease formula and vocabulary complexity metrics.

3. **Word Analysis**  
   Analyze word occurrences, unique words, and text patterns.

4. **Reading Time Estimation**  
   Estimate reading time based on average reading speed with punctuation adjustments.

5. **Multi-language Support**  
   Includes support for Polish characters and diacritics in text analysis.

## Usage

### Basic Text Counting

```csharp
using Zonit.Extensions.Text;

string sampleText = "This is a sample text for analysis. It contains multiple sentences!";

// Get text counter
var counter = Text.Count(sampleText);

Console.WriteLine($"Characters: {counter.Characters}");
Console.WriteLine($"Words: {counter.Words}");
Console.WriteLine($"Letters: {counter.Letters}");
Console.WriteLine($"Numbers: {counter.Numbers}");
Console.WriteLine($"Special Characters: {counter.SpecialChars}");
Console.WriteLine($"Sentences: {counter.Sentences}");
Console.WriteLine($"Paragraphs: {counter.Paragraphs}");
```

### Text Analysis and Readability

```csharp
using Zonit.Extensions.Text;

string article = @"
    Artificial intelligence is transforming the way we work and live. 
    Machine learning algorithms can process vast amounts of data quickly. 
    This technology enables computers to learn and make decisions autonomously.
";

// Get text analyzer
var analyzer = Text.Analyzer(article);

// Reading time estimation
Console.WriteLine($"Estimated reading time: {analyzer.ReadingTime}");

// Readability analysis
Console.WriteLine($"Readability score: {analyzer.ReadabilityScore:F1}");
Console.WriteLine($"Vocabulary complexity: {analyzer.VocabularyComplexity:F1}");

// Word frequency analysis
var wordOccurrences = analyzer.CountWordOccurrences(caseSensitive: false);
foreach (var word in wordOccurrences.Take(5))
{
    Console.WriteLine($"'{word.Key}': {word.Value} times");
}
```

### Advanced Usage Examples

#### Word Frequency Analysis
```csharp
var text = "The quick brown fox jumps over the lazy dog. The dog was really lazy.";
var analyzer = Text.Analyzer(text);

// Case-insensitive word counting
var wordCounts = analyzer.CountWordOccurrences(caseSensitive: false);

// Find most common words
var mostCommon = wordCounts
    .OrderByDescending(x => x.Value)
    .Take(3);

foreach (var word in mostCommon)
{
    Console.WriteLine($"'{word.Key}': {word.Value} occurrences");
}
```

#### Reading Time with Custom Settings
```csharp
var longArticle = "Your long article content here...";
var analyzer = Text.Analyzer(longArticle);

// Get estimated reading time (default: 200 words per minute)
var readingTime = analyzer.ReadingTime;

Console.WriteLine($"Estimated reading time: {readingTime.Minutes} minutes, {readingTime.Seconds} seconds");
```

#### Readability Assessment
```csharp
var content = "Your content to analyze...";
var analyzer = Text.Analyzer(content);

var readabilityScore = analyzer.ReadabilityScore;
var complexityScore = analyzer.VocabularyComplexity;

// Interpret readability score (Flesch Reading Ease)
string readabilityLevel = readabilityScore switch
{
    >= 90 => "Very Easy",
    >= 80 => "Easy", 
    >= 70 => "Fairly Easy",
    >= 60 => "Standard",
    >= 50 => "Fairly Difficult",
    >= 30 => "Difficult",
    _ => "Very Difficult"
};

Console.WriteLine($"Readability: {readabilityLevel} (Score: {readabilityScore:F1})");
Console.WriteLine($"Vocabulary Complexity: {complexityScore:F1}/100");
```

## API Reference

### Text Static Class

The main entry point for text analysis operations:

```csharp
public static class Text
{
    public static TextCounter Count(string text);
    public static TextAnalyzer Analyzer(string text);
}
```

### TextCounter Properties

Basic text statistics:

```csharp
public int Characters { get; }          // Total character count
public int Words { get; }               // Total word count  
public int Letters { get; }             // Letter count only
public int Numbers { get; }             // Digit count
public int SpecialChars { get; }        // Special character count
public int Sentences { get; }           // Sentence count
public int Paragraphs { get; }          // Paragraph count
```

### TextAnalyzer Properties and Methods

Advanced text analysis:

```csharp
public TimeSpan ReadingTime { get; }                           // Estimated reading time
public double ReadabilityScore { get; }                       // Flesch Reading Ease score (0-100)
public double VocabularyComplexity { get; }                   // Vocabulary complexity score (0-100)

// Word analysis methods
public Dictionary<string, int> CountWordOccurrences(bool caseSensitive = false);
```

## Readability Scores Interpretation

### Flesch Reading Ease Scale
- **90-100**: Very Easy to read (5th grade level)
- **80-89**: Easy to read (6th grade level)
- **70-79**: Fairly Easy to read (7th grade level)
- **60-69**: Standard (8th & 9th grade level)
- **50-59**: Fairly Difficult to read (10th to 12th grade level)
- **30-49**: Difficult to read (college level)
- **0-29**: Very Difficult to read (graduate level)

### Vocabulary Complexity
- **0-25**: Simple vocabulary
- **26-50**: Moderate vocabulary
- **51-75**: Complex vocabulary  
- **76-100**: Very complex vocabulary

## Implementation Notes

- **Performance**: All methods are optimized for performance with large text inputs
- **Unicode Support**: Full support for Unicode characters and diacritics
- **Polish Language**: Special support for Polish characters (ąęółśźćń) in syllable counting
- **Thread Safety**: All classes are immutable and thread-safe
- **Memory Efficient**: Minimal memory allocation during text processing

## Use Cases

- **Content Management Systems**: Analyze article readability and complexity
- **Educational Tools**: Assess text difficulty for different reading levels  
- **SEO Analysis**: Evaluate content quality and reading time for web pages
- **Document Processing**: Extract statistics from large text documents
- **Language Learning**: Analyze text complexity for language learners
- **Writing Tools**: Provide real-time feedback on writing quality