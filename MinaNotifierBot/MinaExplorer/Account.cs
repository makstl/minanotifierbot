using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MinaNotifierBot.MinaExplorer;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

public class Account
{
    public string publicKey { get; set; }
    public Balance balance { get; set; }
    public int nonce { get; set; }
    public string receiptChainHash { get; set; }
    public string @delegate { get; set; }
    public string votingFor { get; set; }
    public int totalTx { get; set; }
    public int totalBlocks { get; set; }
    public int totalSnarks { get; set; }
    public int countPendingTransactions { get; set; }
    //public List<object> firstPendingTransaction { get; set; }
    public string username { get; set; }
    //public List<EpochStakingAccount> epochStakingAccount { get; set; }
    //public List<NextEpochStakingAccount> nextEpochStakingAccount { get; set; }
    public List<EpochDelegator> epochDelegators { get; set; }
    //public List<object> nextEpochDelegators { get; set; }
    public decimal epochTotalStakingBalance { get; set; }
    public decimal nextEpochTotalStakingBalance { get; set; }
}
public class EpochDelegator
{
    public string pk { get; set; }
    public string balance { get; set; }
    public string @delegate { get; set; }
    public int token { get; set; }
    public int nonce { get; set; }
    public string receipt_chain_hash { get; set; }
    public string voting_for { get; set; }
    public int epoch { get; set; }
    public string chainId { get; set; }
    public string ledgerHash { get; set; }
    public string public_key { get; set; }
}
public class Balance
{
    public decimal total { get; set; }
    public string unknown { get; set; }
    public int blockHeight { get; set; }
    public decimal? lockedBalance { get; set; }
    public string stateHash { get; set; }
    public string liquid { get; set; }
    public string locked { get; set; }
}

public class EpochStakingAccount
{
    public string pk { get; set; }
    public string balance { get; set; }
    public string @delegate { get; set; }
    public int token { get; set; }
    public int nonce { get; set; }
    public string receipt_chain_hash { get; set; }
    public string voting_for { get; set; }
    public int epoch { get; set; }
    public string chainId { get; set; }
    public string ledgerHash { get; set; }
    public string public_key { get; set; }
}

public class NextEpochStakingAccount
{
    public string pk { get; set; }
    public string balance { get; set; }
    public string @delegate { get; set; }
    public int token { get; set; }
    public int nonce { get; set; }
    public string receipt_chain_hash { get; set; }
    public string voting_for { get; set; }
    public string ledgerHash { get; set; }
    public string public_key { get; set; }
}

public class AccountResult
{
    public Account account { get; set; }
    public Status status { get; set; }
}

public class Status
{
    public string syncStatus { get; set; }
    public int blockchainLength { get; set; }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
