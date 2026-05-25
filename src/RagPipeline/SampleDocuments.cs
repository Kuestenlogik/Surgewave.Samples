#pragma warning disable CA1024 // Use properties where appropriate

namespace Kuestenlogik.Surgewave.Samples.RagPipeline;

/// <summary>
/// Sample documents for the RAG pipeline demo.
/// </summary>
public static class SampleDocuments
{
    public static IReadOnlyList<Document> GetTechDocuments() =>
    [
        new Document
        {
            Id = "doc-001",
            Title = "Introduction to Apache Kafka",
            Content = "Apache Kafka is a distributed event streaming platform capable of handling trillions of events a day. " +
                "Originally developed by LinkedIn, Kafka is now an open-source project maintained by the Apache Software Foundation. " +
                "It is designed for high-throughput, fault-tolerant messaging and is commonly used for building real-time data pipelines " +
                "and streaming applications. Kafka uses a publish-subscribe model where producers write data to topics and consumers " +
                "read from those topics. Each topic is divided into partitions for scalability.",
            Source = "tech-docs",
            Category = "Messaging"
        },
        new Document
        {
            Id = "doc-002",
            Title = "Understanding Event Sourcing",
            Content = "Event sourcing is an architectural pattern where state changes are stored as a sequence of events rather than " +
                "just the current state. Instead of updating a database record in place, each change is appended as an immutable event. " +
                "This provides a complete audit trail, enables temporal queries (time travel), and supports rebuilding state by " +
                "replaying events. Event sourcing is commonly used with CQRS (Command Query Responsibility Segregation) and works " +
                "well with message queues like Kafka for event distribution.",
            Source = "tech-docs",
            Category = "Architecture"
        },
        new Document
        {
            Id = "doc-003",
            Title = "Vector Databases and Embeddings",
            Content = "Vector databases are specialized databases optimized for storing and querying high-dimensional vectors (embeddings). " +
                "Embeddings are numerical representations of data (text, images, audio) that capture semantic meaning. Similar items " +
                "have similar embeddings, enabling semantic search where queries find conceptually related content rather than just " +
                "keyword matches. Popular vector databases include Qdrant, Pinecone, Milvus, and Weaviate. They use algorithms like " +
                "HNSW (Hierarchical Navigable Small World) for efficient approximate nearest neighbor search.",
            Source = "tech-docs",
            Category = "AI/ML"
        },
        new Document
        {
            Id = "doc-004",
            Title = "Retrieval-Augmented Generation (RAG)",
            Content = "RAG is a technique that combines retrieval systems with generative AI models. Instead of relying solely on the " +
                "LLM's training data, RAG retrieves relevant documents from a knowledge base and includes them in the prompt context. " +
                "This approach reduces hallucinations, provides up-to-date information, and enables AI systems to answer questions " +
                "about private or domain-specific data. A typical RAG pipeline includes: document ingestion, chunking, embedding " +
                "generation, vector storage, semantic retrieval, and LLM generation with retrieved context.",
            Source = "tech-docs",
            Category = "AI/ML"
        },
        new Document
        {
            Id = "doc-005",
            Title = "Microservices Communication Patterns",
            Content = "Microservices architectures rely on various communication patterns. Synchronous patterns include REST APIs and " +
                "gRPC for request-response interactions. Asynchronous patterns use message queues (RabbitMQ, Kafka) for event-driven " +
                "communication. The saga pattern coordinates transactions across services using choreography or orchestration. " +
                "Service mesh technologies like Istio handle cross-cutting concerns like load balancing, circuit breaking, and " +
                "observability. Choosing the right pattern depends on latency requirements, coupling, and reliability needs.",
            Source = "tech-docs",
            Category = "Architecture"
        },
        new Document
        {
            Id = "doc-006",
            Title = "Introduction to Surgewave",
            Content = "Surgewave is a high-performance, drop-in replacement for Apache Kafka built with modern .NET. It provides " +
                "wire-compatible Kafka protocol support, allowing existing Kafka clients to connect without modification. " +
                "Surgewave's native protocol achieves significantly lower latency than Kafka - benchmarks show 345x improvement. " +
                "Key features include multi-broker clustering with Raft consensus, tiered storage (S3, Azure, GCP), " +
                "Apache Arrow columnar storage, and a comprehensive Kafka Connect-compatible connector framework.",
            Source = "tech-docs",
            Category = "Messaging"
        },
        new Document
        {
            Id = "doc-007",
            Title = "OpenAI Embeddings API",
            Content = "OpenAI's text-embedding models convert text into numerical vectors that capture semantic meaning. " +
                "The text-embedding-3-small model offers a good balance of performance and cost, producing 1536-dimensional vectors. " +
                "The text-embedding-3-large model provides higher quality embeddings with configurable dimensions up to 3072. " +
                "These embeddings are useful for semantic search, clustering, classification, and recommendation systems. " +
                "The API supports batch processing for efficiency and handles up to 8191 tokens per input.",
            Source = "tech-docs",
            Category = "AI/ML"
        },
        new Document
        {
            Id = "doc-008",
            Title = "Consumer Groups in Kafka",
            Content = "Consumer groups enable parallel processing of Kafka topics. Multiple consumers in a group share the work " +
                "of reading from topic partitions - each partition is assigned to exactly one consumer in the group. This provides " +
                "both scalability and fault tolerance. If a consumer fails, its partitions are reassigned to other group members " +
                "(rebalancing). Consumers commit offsets to track their progress, supporting at-least-once delivery semantics. " +
                "Different consumer groups can independently read the same topic for different use cases.",
            Source = "tech-docs",
            Category = "Messaging"
        },
        new Document
        {
            Id = "doc-009",
            Title = "Qdrant Vector Database",
            Content = "Qdrant is an open-source vector database written in Rust, designed for production-ready vector similarity search. " +
                "It supports filtering with payloads, allowing hybrid search combining vector similarity with metadata constraints. " +
                "Qdrant offers both gRPC and REST APIs, with clients available for Python, JavaScript, Go, and .NET. " +
                "It uses HNSW algorithm for efficient approximate nearest neighbor search and supports various distance metrics " +
                "including cosine similarity, Euclidean distance, and dot product.",
            Source = "tech-docs",
            Category = "AI/ML"
        },
        new Document
        {
            Id = "doc-010",
            Title = "Stream Processing with Kafka Streams",
            Content = "Kafka Streams is a client library for building stream processing applications. It provides high-level DSL " +
                "operations like map, filter, groupBy, and join. State stores (RocksDB-backed by default) enable stateful processing " +
                "like aggregations and joins. Windowing supports tumbling, hopping, sliding, and session windows for time-based " +
                "grouping. Kafka Streams handles fault tolerance through changelog topics that back state stores, enabling " +
                "exactly-once semantics when configured with transactions.",
            Source = "tech-docs",
            Category = "Messaging"
        }
    ];
}
